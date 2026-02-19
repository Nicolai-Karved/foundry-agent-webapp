import React, { useState, useMemo, useRef, useEffect } from 'react';
import { ChatInterface } from './ChatInterface';
import { SettingsPanel } from './core/SettingsPanel';
import { DocumentViewer } from './DocumentViewer';
import { TaskPanel } from './TaskPanel';
import { useAppState } from '../hooks/useAppState';
import { useAuth } from '../hooks/useAuth';
import { ChatService } from '../services/chatService';
import { useAppContext } from '../contexts/AppContext';
import { DocumentStandardsPanel } from './core/DocumentStandardsPanel';
import { RoutingModePanel } from './core/RoutingModePanel';
import styles from './AgentPreview.module.css';

interface AgentPreviewProps {
  agentId: string;
  agentName: string;
  agentDescription?: string;
  agentLogo?: string;
  starterPrompts?: string[];
}

export const AgentPreview: React.FC<AgentPreviewProps> = ({ agentId: _agentId, agentName, agentDescription, agentLogo, starterPrompts }) => {
  const { chat, settings } = useAppState();
  const { dispatch } = useAppContext();
  const { getAccessToken } = useAuth();
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [highlightText, setHighlightText] = useState<string | string[] | null>(null);
  const [highlightFallbackText, setHighlightFallbackText] = useState<string | string[] | null>(null);
  const [highlightSeverity, setHighlightSeverity] = useState<string | null>(null);
  const [docRatio, setDocRatio] = useState(0.4);
  const [isDragging, setIsDragging] = useState(false);
  const [handleLeft, setHandleLeft] = useState<number | null>(null);
  const contentRef = useRef<HTMLDivElement | null>(null);

  // Create service instances
  const apiUrl = import.meta.env.VITE_API_URL || '/api';
  
  const chatService = useMemo(() => {
    return new ChatService(apiUrl, getAccessToken, dispatch);
  }, [apiUrl, getAccessToken, dispatch]);

  const latestStructured = useMemo(() => {
    const latest = [...chat.messages]
      .reverse()
      .find((message) => message.role === 'assistant' && message.structured);

    return latest?.structured;
  }, [chat.messages]);

  const latestAttachments = useMemo(() => {
    const latestMessage = [...chat.messages]
      .reverse()
      .find((message) => message.role === 'user' && message.attachments && message.attachments.length > 0);

    return latestMessage?.attachments;
  }, [chat.messages]);

  const handleTaskSelect = (
    task: Record<string, unknown>,
    reference?: string | string[],
    fallbackReference?: string | string[],
    severity?: string
  ) => {
    const taskId = typeof task.id === 'string' ? task.id : typeof task.name === 'string' ? task.name : null;
    setSelectedTaskId(taskId);
    if (reference) {
      setHighlightText(reference);
    }
    if (fallbackReference) {
      setHighlightFallbackText(fallbackReference);
    }
    if (severity) {
      setHighlightSeverity(severity);
    }
  };

  const handleSendMessage = async (text: string, files?: File[]) => {
    await chatService.sendMessage(
      text,
      chat.currentConversationId,
      files,
      settings.selectedStandards,
      settings.agentRouteOverride
    );
  };

  const handleClearError = () => {
    chatService.clearError();
  };

  const handleNewChat = () => {
    chatService.clearChat();
  };

  const handleCancelStream = () => {
    chatService.cancelStream();
  };

  const handleMcpApproval = async (
    approvalRequestId: string,
    approved: boolean,
    previousResponseId: string,
    conversationId: string
  ) => {
    await chatService.sendMcpApproval(approvalRequestId, approved, previousResponseId, conversationId);
  };

  const taskRatio = 0.2;
  const minDocRatio = 0.4;
  const maxDocRatio = 0.6;
  const chatRatio = Math.max(0.2, 1 - taskRatio - docRatio);

  const updateHandlePosition = () => {
    if (!contentRef.current) return;
    const rect = contentRef.current.getBoundingClientRect();
    const style = window.getComputedStyle(contentRef.current);
    const paddingLeft = parseFloat(style.paddingLeft) || 0;
    const paddingRight = parseFloat(style.paddingRight) || 0;
    const gap = parseFloat(style.columnGap) || 0;
    const innerWidth = rect.width - paddingLeft - paddingRight - gap * 2;
    const left = paddingLeft + innerWidth * docRatio;
    setHandleLeft(left);
  };

  useEffect(() => {
    updateHandlePosition();
    const onResize = () => updateHandlePosition();
    window.addEventListener('resize', onResize);
    return () => window.removeEventListener('resize', onResize);
  }, [docRatio]);

  useEffect(() => {
    if (!isDragging) return undefined;

    const handlePointerMove = (event: PointerEvent) => {
      if (!contentRef.current) return;
      const rect = contentRef.current.getBoundingClientRect();
      const style = window.getComputedStyle(contentRef.current);
      const paddingLeft = parseFloat(style.paddingLeft) || 0;
      const paddingRight = parseFloat(style.paddingRight) || 0;
      const gap = parseFloat(style.columnGap) || 0;
      const innerWidth = rect.width - paddingLeft - paddingRight - gap * 2;
      const x = event.clientX - rect.left - paddingLeft;
      const ratio = x / innerWidth;
      const clamped = Math.min(maxDocRatio, Math.max(minDocRatio, ratio));
      setDocRatio(clamped);
    };

    const handlePointerUp = () => {
      setIsDragging(false);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);

    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [isDragging]);

  const handlePointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(true);
  };

  return (
    <div
      ref={contentRef}
      className={styles.content}
      style={{
        ['--doc-col' as string]: `${docRatio * 100}%`,
        ['--chat-col' as string]: `${chatRatio * 100}%`,
        ['--task-col' as string]: `${taskRatio * 100}%`,
      }}
    >
      <aside className={styles.documentPanel}>
        <div className={styles.documentPanelInner}>
          <div className={styles.documentPanelScroll}>
            <DocumentViewer
              attachments={latestAttachments}
              highlightText={highlightText}
              highlightFallbackText={highlightFallbackText}
              highlightSeverity={highlightSeverity}
            />
          </div>
        </div>
      </aside>

      <div
        className={styles.resizeHandle}
        onPointerDown={handlePointerDown}
        role="separator"
        aria-label="Resize document and chat panels"
        aria-orientation="vertical"
        style={handleLeft !== null ? { left: `${handleLeft}px` } : undefined}
      >
        <div className={styles.resizeHandleBar} />
      </div>

      <div className={styles.mainContent}>
        <ChatInterface 
          messages={chat.messages}
          status={chat.status}
          error={chat.error}
          streamingMessageId={chat.streamingMessageId}
          onSendMessage={handleSendMessage}
          onClearError={handleClearError}
          onOpenSettings={() => setIsSettingsOpen(true)}
          onNewChat={handleNewChat}
          onCancelStream={handleCancelStream}
          onMcpApproval={handleMcpApproval}
          conversationId={chat.currentConversationId}
          hasMessages={chat.messages.length > 0}
          disabled={false}
          agentName={agentName}
          agentDescription={agentDescription}
          agentLogo={agentLogo}
          starterPrompts={starterPrompts}
        />
      </div>

      <aside className={styles.taskPanel}>
        <div className={styles.taskPanelInner}>
          <div className={styles.taskPanelScroll}>
            <RoutingModePanel />
            <DocumentStandardsPanel collapseWhenViewingDocument={Boolean(latestAttachments && latestAttachments.length > 0)} />
            <TaskPanel
              tasks={latestStructured?.tasks ?? []}
              title="Tasks"
              subtitle={latestStructured?.documentName}
              selectedTaskId={selectedTaskId}
              onSelectTask={handleTaskSelect}
            />
          </div>
        </div>
      </aside>
      
      <SettingsPanel
        isOpen={isSettingsOpen}
        onOpenChange={setIsSettingsOpen}
      />
    </div>
  );
};
