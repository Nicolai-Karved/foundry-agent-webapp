import React, { useState, useMemo } from 'react';
import { ChatInterface } from './ChatInterface';
import { SettingsPanel } from './core/SettingsPanel';
import { TaskPanel } from './TaskPanel';
import { useAppState } from '../hooks/useAppState';
import { useAuth } from '../hooks/useAuth';
import { ChatService } from '../services/chatService';
import { useAppContext } from '../contexts/AppContext';
import styles from './AgentPreview.module.css';

interface AgentPreviewProps {
  agentId: string;
  agentName: string;
  agentDescription?: string;
  agentLogo?: string;
  starterPrompts?: string[];
}

export const AgentPreview: React.FC<AgentPreviewProps> = ({ agentId: _agentId, agentName, agentDescription, agentLogo, starterPrompts }) => {
  const { chat } = useAppState();
  const { dispatch } = useAppContext();
  const { getAccessToken } = useAuth();
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);

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

  const handleSendMessage = async (text: string, files?: File[]) => {
    await chatService.sendMessage(text, chat.currentConversationId, files);
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

  return (
    <div className={styles.content}>
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
        <div className={styles.taskPanelScroll}>
          <TaskPanel
            tasks={latestStructured?.tasks ?? []}
            title="Tasks"
            subtitle={latestStructured?.documentName}
          />
        </div>
      </aside>
      
      <SettingsPanel
        isOpen={isSettingsOpen}
        onOpenChange={setIsSettingsOpen}
      />
    </div>
  );
};
