import type { Dispatch } from 'react';
import { SpanStatusCode, trace } from '@opentelemetry/api';
import { appTracer, tracingContext } from '../telemetry/otel';
import type { AppAction } from '../types/appState';
import type { IAnnotation, IChatItem, IStructuredResponse } from '../types/chat';
import type { AppError } from '../types/errors';
import type { AgentRouteOverride, StandardSelection } from '../types/standards';
import { isAppError } from '../types/errors';
import {
  createAppError,
  getErrorCodeFromMessage,
  parseErrorFromResponse,
  getErrorCodeFromResponse,
  isTokenExpiredError,
  retryWithBackoff,
} from '../utils/errorHandler';
import {
  convertFilesToDataUris,
  createAttachmentMetadata,
} from '../utils/fileAttachments';
import { parseSseLine, splitSseBuffer } from '../utils/sseParser';

/**
 * ChatService handles all chat-related API operations.
 * Dispatches AppContext actions for state management.
 * 
 * @example
 * ```typescript
 * const chatService = new ChatService(
 *   '/api',
 *   getAccessToken,
 *   dispatch
 * );
 * 
 * // Send a message with images
 * await chatService.sendMessage(
 *   'Analyze this image',
 *   currentThreadId,
 *   [imageFile]
 * );
 * ```
 */
export class ChatService {
  private apiUrl: string;
  private getAccessToken: () => Promise<string | null>;
  private dispatch: Dispatch<AppAction>;
  private currentStreamAbort?: AbortController;
  // Flag indicating an intentional user cancellation of the active stream.
  private streamCancelled = false;

  private static toError(value: unknown): Error {
    if (value instanceof Error) {
      return value;
    }

    return new Error(typeof value === 'string' ? value : JSON.stringify(value));
  }

  private createStreamAppError(code: string | undefined, message: string): AppError {
    const normalized = (code ?? '').toUpperCase();

    if (normalized === 'AUTH_REQUIRED') {
      return createAppError(new Error(message), 'AUTH');
    }

    if (normalized === 'STANDARDS_EMPTY') {
      return {
        code: 'STREAM',
        message,
        recoverable: true,
        originalError: new Error(message),
      };
    }

    return createAppError(new Error(message), 'STREAM');
  }

  constructor(
    apiUrl: string,
    getAccessToken: () => Promise<string | null>,
    dispatch: Dispatch<AppAction>
  ) {
    this.apiUrl = apiUrl;
    this.getAccessToken = getAccessToken;
    this.dispatch = dispatch;
  }

  /**
   * Acquire authentication token using MSAL.
   * Attempts silent acquisition first, falls back to popup if needed.
   * 
   * @returns Access token string
   * @throws {Error} If token acquisition fails
   */
  private async ensureAuthToken(): Promise<string> {
    const token = await this.getAccessToken();
    if (!token) {
      throw createAppError(new Error('Failed to acquire access token'), 'AUTH');
    }
    return token;
  }

  /**
   * Prepare message payload with optional file attachments.
   * Converts files to data URIs and separates images from documents.
   * 
   * @param text - Message text content
   * @param files - Optional array of files (images and documents)
   * @returns Payload with content, image URIs, file attachments, and attachment metadata
   */
  private async prepareMessagePayload(
    text: string,
    files?: File[]
  ): Promise<{
    content: string;
    imageDataUris: string[];
    fileDataUris: Array<{ dataUri: string; fileName: string; mimeType: string }>;
    attachments: IChatItem['attachments'];
  }> {
    let imageDataUris: string[] = [];
    let fileDataUris: Array<{ dataUri: string; fileName: string; mimeType: string }> = [];
    let attachments: IChatItem['attachments'] = undefined;

    if (files && files.length > 0) {
      try {
        const results = await convertFilesToDataUris(files);
        
        // Separate images from documents
        const imageResults = results.filter((r) => r.mimeType.startsWith('image/'));
        const fileResults = results.filter((r) => !r.mimeType.startsWith('image/'));
        
        imageDataUris = imageResults.map((r) => r.dataUri);
        fileDataUris = fileResults.map((r) => ({
          dataUri: r.dataUri,
          fileName: r.name,
          mimeType: r.mimeType,
        }));
        
        // Create attachment metadata for UI display
        attachments = createAttachmentMetadata(results);
      } catch (error) {
        const appError = createAppError(error);
        this.dispatch({ type: 'CHAT_ERROR', error: appError });
        throw appError;
      }
    }

    return { content: text, imageDataUris, fileDataUris, attachments };
  }

  /**
   * Construct request body for chat API.
   * 
   * @param message - User message text
   * @param conversationId - Current conversation ID (null for new conversations)
   * @param imageDataUris - Array of base64 data URIs for images
   * @param fileDataUris - Array of file attachments with metadata
   * @returns Request body object
   */
  private constructRequestBody(
    message: string,
    conversationId: string | null,
    imageDataUris: string[],
    fileDataUris: Array<{ dataUri: string; fileName: string; mimeType: string }>,
    standardsSelected?: StandardSelection[],
    agentRouteHint?: 'air' | 'eir' | 'bep'
  ): Record<string, any> {
    return {
      message,
      conversationId,
      imageDataUris: imageDataUris.length > 0 ? imageDataUris : undefined,
      fileDataUris: fileDataUris.length > 0 ? fileDataUris : undefined,
      standardsSelected: standardsSelected && standardsSelected.length > 0 ? standardsSelected : undefined,
      agentRouteHint,
    };
  }

  private detectAgentRouteHint(
    files?: File[],
    standardsSelected?: StandardSelection[]
  ): 'air' | 'eir' | 'bep' | undefined {
    const normalizedNames = (files ?? []).map((f) => f.name.toLowerCase());

    const hasAirFile = normalizedNames.some((name) => name.includes('air'));
    const hasEirFile = normalizedNames.some((name) => name.includes('eir'));
    const hasBepFile = normalizedNames.some((name) => name.includes('bep'));

    if (hasBepFile && hasAirFile && hasEirFile) {
      return 'bep';
    }

    if (hasEirFile) {
      return 'eir';
    }

    if (hasAirFile) {
      return 'air';
    }

    const standardIds = (standardsSelected ?? [])
      .map((s) => s.standardId.toLowerCase());

    if (standardIds.some((id) => id.includes('eir'))) {
      return 'eir';
    }

    if (standardIds.some((id) => id.includes('air'))) {
      return 'air';
    }

    return undefined;
  }

  private resolveAgentRouteHint(
    routeOverride: AgentRouteOverride | undefined,
    files?: File[],
    standardsSelected?: StandardSelection[]
  ): 'air' | 'eir' | 'bep' | undefined {
    if (routeOverride && routeOverride !== 'auto') {
      return routeOverride;
    }

    return this.detectAgentRouteHint(files, standardsSelected);
  }

  /**
   * Initiate streaming fetch request to chat API.
   * Validates response and throws typed errors on failure.
   * 
   * @param url - API endpoint URL
   * @param token - Access token
   * @param body - Request body
   * @param signal - Abort signal for cancellation
   * @returns Response object
   * @throws {AppError} If request fails or response is not OK
   */
  private async initiateStream(
    url: string,
    token: string,
    body: Record<string, any>,
    signal: AbortSignal
  ): Promise<Response> {
    const res = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(body),
      signal,
    });

    if (!res.ok) {
      const errorMessage = await parseErrorFromResponse(res);
      const errorCode = getErrorCodeFromResponse(res);
      throw createAppError(new Error(errorMessage), errorCode);
    }

    return res;
  }

  /**
   * Send a message and stream the response from the Azure AI Agent.
   * Orchestrates authentication, file conversion, optimistic UI updates, and streaming.
   * 
   * @param messageText - The user's message text
   * @param currentConversationId - Current conversation ID (null for new conversations)
   * @param files - Optional array of files to attach (images and documents)
   * @throws {Error} If authentication fails or API request fails
   * 
   * @remarks
   * Token acquisition: Attempts acquireTokenSilent first, falls back to acquireTokenPopup.
   * Retries failed requests up to 3 times with exponential backoff.
   */
  async sendMessage(
    messageText: string,
    currentConversationId: string | null,
    files?: File[],
    standardsSelected?: StandardSelection[],
    routeOverride?: AgentRouteOverride
  ): Promise<void> {
    const span = appTracer.startSpan('chat.send', {
      attributes: {
        'app.message.length': messageText.length,
        'app.attachments.count': files?.length ?? 0,
        'app.standards.count': standardsSelected?.length ?? 0,
        'app.conversation.has_id': Boolean(currentConversationId),
      },
    });

    return tracingContext.with(trace.setSpan(tracingContext.active(), span), async () => {
      if (this.currentStreamAbort) {
        this.streamCancelled = true;
        this.currentStreamAbort.abort();
        this.dispatch({ type: 'CHAT_CANCEL_STREAM' });
      }

      try {
        const token = await this.ensureAuthToken();
        const { content, imageDataUris, fileDataUris, attachments } = await this.prepareMessagePayload(
          messageText,
          files
        );

      const userMessage: IChatItem = {
        id: Date.now().toString(),
        role: 'user',
        content,
        attachments,
        more: {
          time: new Date().toISOString(),
        },
      };

      this.dispatch({ type: 'CHAT_SEND_MESSAGE', message: userMessage });

      const assistantMessageId = (Date.now() + 1).toString();
      this.dispatch({ type: 'CHAT_ADD_ASSISTANT_MESSAGE', messageId: assistantMessageId });
      this.dispatch({
        type: 'CHAT_START_STREAM',
        conversationId: currentConversationId || undefined,
        messageId: assistantMessageId,
      });

      this.currentStreamAbort = new AbortController();
      this.streamCancelled = false;

      const requestBody = this.constructRequestBody(
        messageText,
        currentConversationId,
        imageDataUris,
        fileDataUris,
        standardsSelected,
        this.resolveAgentRouteHint(routeOverride, files, standardsSelected)
      );

      const response = await retryWithBackoff(
        async () =>
          this.initiateStream(
            `${this.apiUrl}/chat/stream`,
            token,
            requestBody,
            this.currentStreamAbort!.signal
          ),
        3,
        1000
      );

        await this.processStream(response, assistantMessageId, currentConversationId);
        this.currentStreamAbort = undefined;
        this.streamCancelled = false;
        span.setStatus({ code: SpanStatusCode.OK });
      } catch (error) {
        if (error instanceof DOMException && error.name === 'AbortError') {
          span.addEvent('stream.aborted');
          span.setStatus({ code: SpanStatusCode.UNSET });
          return;
        }

        if (isTokenExpiredError(error)) {
          this.dispatch({ type: 'AUTH_TOKEN_EXPIRED' });
        }

        const appError: AppError = isAppError(error)
          ? error
          : createAppError(
              error,
              getErrorCodeFromMessage(error),
              () => this.sendMessage(messageText, currentConversationId, files, standardsSelected, routeOverride)
            );

        span.recordException(ChatService.toError(error));
        span.setStatus({ code: SpanStatusCode.ERROR });
        this.dispatch({ type: 'CHAT_ERROR', error: appError });
        throw error;
      } finally {
        span.end();
      }
    });
  }

  /**
   * Process Server-Sent Events stream from the API.
   * Implements duplicate chunk suppression to prevent UI flicker.
   * 
   * @param response - Fetch Response object with SSE stream
   * @param messageId - ID of the assistant message being streamed
   * @param currentConversationId - Current conversation ID (null for new conversations)
   * @throws {Error} If stream is not readable or parsing fails
   */
  private async processStream(
    response: Response,
    messageId: string,
    currentConversationId: string | null
  ): Promise<void> {
    const span = appTracer.startSpan('chat.stream', {
      attributes: {
        'app.message.id': messageId,
        'app.conversation.has_id': Boolean(currentConversationId),
      },
    });

    const reader = response.body?.getReader();
    const decoder = new TextDecoder();

    if (!reader) {
      const error = createAppError(
        new Error(`Response body is not readable for message ${messageId}`),
        'STREAM'
      );
      this.dispatch({ type: 'CHAT_ERROR', error });
      span.recordException(ChatService.toError(error));
      span.setStatus({ code: SpanStatusCode.ERROR });
      throw error;
    }

    let newConversationId = currentConversationId;
    let lastChunkContent: string | undefined;
    let buffer = '';
    let structuredBuffer = '';
    let structuredMode = false;
    let placeholderSet = false;
    const collectedAnnotations: IAnnotation[] = [];
    const autoApproveMcp = true;

    const setPlaceholder = () => {
      if (placeholderSet) return;
      this.dispatch({
        type: 'CHAT_SET_MESSAGE_CONTENT',
        messageId,
        content: 'Retrieving response...',
      });
      placeholderSet = true;
    };

    const finalizeStructured = () => {
      const structured = this.parseStructuredResponse(structuredBuffer);
      if (structured) {
        this.dispatch({
          type: 'CHAT_SET_MESSAGE_STRUCTURED',
          messageId,
          content: structured.response,
          structured,
          annotations: collectedAnnotations.length > 0 ? collectedAnnotations : undefined,
        });
        return;
      }

      if (structuredBuffer.trim()) {
        this.dispatch({
          type: 'CHAT_SET_MESSAGE_CONTENT',
          messageId,
          content: structuredBuffer,
        });
      }
    };

    try {
      while (true) {
        if (this.streamCancelled) {
          break;
        }

        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        buffer += chunk;

        const [lines, remaining] = splitSseBuffer(buffer);
        buffer = remaining;

        for (const line of lines) {
          const event = parseSseLine(line);
          if (!event) continue;

          if (event.data?.error) {
            console.error('[ChatService] SSE error event received:', event.data.error);
            const errorMessage = event.data.error.message || event.data.error || 'Stream error occurred';
            const errorCode = typeof event.data.error.code === 'string' ? event.data.error.code : undefined;
            const error = this.createStreamAppError(errorCode, errorMessage);
            if (error.code === 'AUTH') {
              this.dispatch({ type: 'AUTH_TOKEN_EXPIRED' });
            }
            this.dispatch({ type: 'CHAT_ERROR', error });
            throw error;
          }

          if (event.type === 'annotations') {
            if (event.data.annotations && event.data.annotations.length > 0) {
              collectedAnnotations.push(...event.data.annotations);
              span.addEvent('annotations.received', {
                'app.annotations.count': event.data.annotations.length,
              });
              this.dispatch({
                type: 'CHAT_STREAM_ANNOTATIONS',
                messageId,
                annotations: event.data.annotations,
              });
            }
            continue;
          }

          switch (event.type) {
            case 'agent':
              if (event.data.agent && typeof event.data.agent.name === 'string') {
                this.dispatch({
                  type: 'CHAT_SET_MESSAGE_AGENT',
                  messageId,
                  agentName: event.data.agent.name,
                  agentRoute: typeof event.data.agent.route === 'string' ? event.data.agent.route : undefined,
                });
              }
              break;

            case 'conversationId':
              if (!newConversationId) {
                newConversationId = event.data.conversationId;
                span.addEvent('conversation.id.received');
                this.dispatch({
                  type: 'CHAT_START_STREAM',
                  conversationId: event.data.conversationId,
                  messageId,
                });
              }
              break;

            case 'chunk':
              if (typeof event.data.content === 'string') {
                structuredBuffer += event.data.content;
              }

              if (!structuredMode && this.isStructuredCandidate(structuredBuffer)) {
                structuredMode = true;
              }

              if (structuredMode) {
                setPlaceholder();
                break;
              }

              if (event.data.content !== lastChunkContent) {
                this.dispatch({
                  type: 'CHAT_STREAM_CHUNK',
                  messageId,
                  content: event.data.content,
                });
                lastChunkContent = event.data.content;
              }
              break;

            case 'mcpApprovalRequest':
              if (event.data.approvalRequest) {
                span.addEvent('mcp.approval.requested');
                if (autoApproveMcp) {
                  const approval = event.data.approvalRequest;
                  if (!approval.previousResponseId) {
                    throw createAppError(
                      new Error('MCP approval responseId missing; cannot auto-approve'),
                      'STREAM'
                    );
                  }
                  const conversationId = newConversationId ?? currentConversationId;
                  if (!conversationId) {
                    throw createAppError(
                      new Error('MCP approval conversationId missing; cannot auto-approve'),
                      'STREAM'
                    );
                  }
                  await this.sendMcpApproval(
                    approval.id,
                    true,
                    approval.previousResponseId,
                    conversationId
                  );
                  return;
                }

                this.dispatch({
                  type: 'CHAT_MCP_APPROVAL_REQUEST',
                  messageId,
                  approvalRequest: event.data.approvalRequest,
                  previousResponseId: event.data.approvalRequest.previousResponseId ?? null,
                });
              }
              break;

            case 'usage':
              span.addEvent('stream.usage');
              this.dispatch({
                type: 'CHAT_STREAM_COMPLETE',
                usage: {
                  promptTokens: event.data.promptTokens,
                  completionTokens: event.data.completionTokens,
                  totalTokens: event.data.totalTokens,
                  duration: event.data.duration,
                },
              });
              break;

            case 'done':
              if (structuredBuffer.trim().length > 0) {
                finalizeStructured();
              }
              span.setStatus({ code: SpanStatusCode.OK });
              return;

            case 'error':
              const errorMessage = `Stream error for message ${messageId}: ${event.data.message}`;
              const error = this.createStreamAppError(event.data.code, errorMessage);
              if (error.code === 'AUTH') {
                this.dispatch({ type: 'AUTH_TOKEN_EXPIRED' });
              }
              this.dispatch({ type: 'CHAT_ERROR', error });
              span.recordException(ChatService.toError(error));
              span.setStatus({ code: SpanStatusCode.ERROR });
              throw error;
          }
        }
      }

      if (structuredBuffer.trim().length > 0) {
        finalizeStructured();
      }
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError' && this.streamCancelled) {
        // User intentionally cancelled the stream - not an error condition
        return;
      }

      const appError =
        error instanceof Error && 'code' in error
          ? error
          : createAppError(
              new Error(
                `Stream processing failed: ${error instanceof Error ? error.message : String(error)} (Conversation: ${currentConversationId}, Message: ${messageId})`
              ),
              'STREAM'
            );
      this.dispatch({ type: 'CHAT_ERROR', error: appError as AppError });
      span.recordException(ChatService.toError(error));
      span.setStatus({ code: SpanStatusCode.ERROR });
      throw error;
    } finally {
      try {
        reader.releaseLock();
      } catch {
        // Reader may already be released
      }
      span.end();
    }
  }

  private isStructuredCandidate(content: string): boolean {
    const trimmed = content.trimStart();
    return trimmed.startsWith('{') || trimmed.startsWith('```');
  }

  private parseStructuredResponse(content: string): IStructuredResponse | null {
    const trimmed = content.trim();
    if (!trimmed) return null;

    const fenceMatch = trimmed.match(/^```(?:json)?\s*([\s\S]*?)\s*```$/i);
    const candidate = fenceMatch ? fenceMatch[1].trim() : trimmed;

    const tryParse = (text: string) => {
      try {
        const parsed = JSON.parse(text) as Record<string, unknown>;
        if (parsed && typeof parsed === 'object') {
          return parsed;
        }
        return null;
      } catch {
        return null;
      }
    };

    let parsed = tryParse(candidate);
    if (!parsed) {
      const start = candidate.indexOf('{');
      const end = candidate.lastIndexOf('}');
      if (start >= 0 && end > start) {
        parsed = tryParse(candidate.slice(start, end + 1));
      }
    }

    if (!parsed) return null;

    const response = typeof parsed.response === 'string' ? parsed.response : null;
    if (!response) return null;

    const tasks = Array.isArray(parsed.tasks)
      ? parsed.tasks.filter((task) => task && typeof task === 'object') as Array<Record<string, unknown>>
      : [];

    return {
      response,
      tasks,
      documentName: typeof parsed.document_name === 'string' ? parsed.document_name : undefined,
      documentId: typeof parsed.id === 'string' ? parsed.id : undefined,
      raw: parsed,
    };
  }

  /**
   * Send approval response for an MCP tool call.
   * 
   * @param approvalRequestId - ID of the approval request
   * @param approved - Whether the tool call was approved
   * @param previousResponseId - Response ID to continue from
   * @param conversationId - Current conversation ID
   */
  async sendMcpApproval(
    approvalRequestId: string,
    approved: boolean,
    previousResponseId: string,
    conversationId: string
  ): Promise<void> {
    const span = appTracer.startSpan('chat.mcp_approval', {
      attributes: {
        'app.mcp.approved': approved,
        'app.conversation.id_present': Boolean(conversationId),
      },
    });

    try {
      const token = await this.ensureAuthToken();

      const assistantMessageId = Date.now().toString();
      this.dispatch({ type: 'CHAT_ADD_ASSISTANT_MESSAGE', messageId: assistantMessageId });
      this.dispatch({
        type: 'CHAT_START_STREAM',
        conversationId,
        messageId: assistantMessageId,
      });

      this.currentStreamAbort = new AbortController();
      this.streamCancelled = false;

      const requestBody = {
        message: approved ? 'Approved' : 'Rejected',
        conversationId,
        previousResponseId,
        mcpApproval: {
          approvalRequestId,
          approved,
        },
      };

      const response = await retryWithBackoff(
        async () =>
          this.initiateStream(
            `${this.apiUrl}/chat/stream`,
            token,
            requestBody,
            this.currentStreamAbort!.signal
          ),
        3,
        1000
      );

      await this.processStream(response, assistantMessageId, conversationId);
      this.currentStreamAbort = undefined;
      this.streamCancelled = false;
      span.setStatus({ code: SpanStatusCode.OK });
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        span.addEvent('stream.aborted');
        span.setStatus({ code: SpanStatusCode.UNSET });
        return;
      }

      const appError: AppError = isAppError(error)
        ? error
        : createAppError(error, getErrorCodeFromMessage(error));

      this.dispatch({ type: 'CHAT_ERROR', error: appError });
      span.recordException(ChatService.toError(error));
      span.setStatus({ code: SpanStatusCode.ERROR });
      throw error;
    } finally {
      span.end();
    }
  }

  /**
   * Clear chat history and reset to empty state.
   * Dispatches CHAT_CLEAR action to remove all messages and conversation ID.
   */
  clearChat(): void {
    this.dispatch({ type: 'CHAT_CLEAR' });
  }

  /**
   * Clear current error state without affecting chat history.
   * Dispatches CHAT_CLEAR_ERROR action.
   */
  clearError(): void {
    this.dispatch({ type: 'CHAT_CLEAR_ERROR' });
  }

  /**
   * Cancel the current streaming response if any is active.
   * Abort controller is not cleared immediately to allow processStream
   * to observe the cancellation flag and exit gracefully.
   */
  cancelStream(): void {
    if (this.currentStreamAbort) {
      const span = appTracer.startSpan('chat.cancel');
      span.addEvent('stream.cancel.requested');
      this.streamCancelled = true;
      this.currentStreamAbort.abort();
      this.dispatch({ type: 'CHAT_CANCEL_STREAM' });
      span.end();
    }
  }
}
