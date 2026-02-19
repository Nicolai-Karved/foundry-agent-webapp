import type { AccountInfo } from '@azure/msal-browser';
import type { IChatItem, IUsageInfo, IAnnotation, IMcpApprovalRequest } from './chat';
import type { AppError } from './errors';
import type { AgentRouteOverride, StandardSelection } from './standards';
import { DEFAULT_SELECTED_STANDARDS } from '../config/standards';

// Re-export types for convenience
export type { IChatItem, IUsageInfo, IAnnotation, IMcpApprovalRequest };

/**
 * Central application state structure
 * All application state flows through this single source of truth
 */
export interface AppState {
  // Authentication state
  auth: {
    status: 'initializing' | 'authenticated' | 'unauthenticated' | 'error';
    user: AccountInfo | null;
    error: string | null;
  };
  
  // Chat operations state
  chat: {
    status: 'idle' | 'sending' | 'streaming' | 'error';
    messages: IChatItem[];
    currentConversationId: string | null;
    error: AppError | null; // Enhanced error object
    streamingMessageId?: string; // Which message is actively streaming
  };
  
  // UI coordination state
  ui: {
    chatInputEnabled: boolean; // Disable during streaming/errors
  };

  // Settings and preferences
  settings: {
    selectedStandards: StandardSelection[];
    agentRouteOverride: AgentRouteOverride;
  };
}

/**
 * All possible actions that can modify application state
 * Use discriminated unions for type safety
 */
export type AppAction = 
  // Auth actions
  | { type: 'AUTH_INITIALIZED'; user: AccountInfo }
  | { type: 'AUTH_TOKEN_EXPIRED' }
  
  // Chat actions
  | { type: 'CHAT_SEND_MESSAGE'; message: IChatItem }
  | { type: 'CHAT_START_STREAM'; conversationId?: string; messageId: string }
  | { type: 'CHAT_STREAM_CHUNK'; messageId: string; content: string }
  | { type: 'CHAT_SET_MESSAGE_CONTENT'; messageId: string; content: string }
  | { type: 'CHAT_SET_MESSAGE_AGENT'; messageId: string; agentName: string; agentRoute?: string }
  | { type: 'CHAT_SET_MESSAGE_STRUCTURED'; messageId: string; content: string; structured: IChatItem['structured']; annotations?: IAnnotation[] }
  | { type: 'CHAT_STREAM_ANNOTATIONS'; messageId: string; annotations: IAnnotation[] }
  | { type: 'CHAT_MCP_APPROVAL_REQUEST'; messageId: string; approvalRequest: IMcpApprovalRequest; previousResponseId: string | null }
  | { type: 'CHAT_STREAM_COMPLETE'; usage: IUsageInfo }
  | { type: 'CHAT_CANCEL_STREAM' }
  | { type: 'CHAT_ERROR'; error: AppError } // Enhanced error object
  | { type: 'CHAT_CLEAR_ERROR' } // Clear error state
  | { type: 'CHAT_CLEAR' }
  | { type: 'CHAT_ADD_ASSISTANT_MESSAGE'; messageId: string }

  // Settings actions
  | { type: 'SET_STANDARDS_SELECTION'; selectedStandards: StandardSelection[] }
  | { type: 'SET_AGENT_ROUTE_OVERRIDE'; agentRouteOverride: AgentRouteOverride };

/**
 * Initial state for the application
 */
export const initialAppState: AppState = {
  auth: {
    status: 'initializing',
    user: null,
    error: null,
  },
  chat: {
    status: 'idle',
    messages: [],
    currentConversationId: null,
    error: null,
    streamingMessageId: undefined,
  },
  ui: {
    chatInputEnabled: true,
  },
  settings: {
    selectedStandards: DEFAULT_SELECTED_STANDARDS,
    agentRouteOverride: 'auto',
  },
};
