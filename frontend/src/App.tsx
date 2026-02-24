import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsalAuthentication } from "@azure/msal-react";
import { Button, Spinner } from '@fluentui/react-components';
import { useAppState } from './hooks/useAppState';
import { InteractionType } from "@azure/msal-browser";
import { ErrorBoundary } from "./components/core/ErrorBoundary";
import { AgentPreview } from "./components/AgentPreview";
import { loginRequest } from "./config/authConfig";
import { useState, useEffect, useCallback } from "react";
import { useAuth } from "./hooks/useAuth";
import type { IAgentMetadata } from "./types/chat";
import { DETAILED_ERROR_MESSAGES } from "./types/errors";
import "./App.css";

function App() {
  // This hook handles authentication automatically - redirects if not authenticated
  useMsalAuthentication(InteractionType.Redirect, loginRequest);
  const { auth } = useAppState();
  const { getAccessToken, isRedirectingForAuth, signInAgain } = useAuth();
  const [agentMetadata, setAgentMetadata] = useState<IAgentMetadata | null>(null);
  const [isLoadingAgent, setIsLoadingAgent] = useState(true);
  const authErrorMessage = DETAILED_ERROR_MESSAGES.AUTH;

  // Wrap fetchAgentMetadata in useCallback to make it stable for the effect
  const fetchAgentMetadata = useCallback(async () => {
    if (auth.status !== 'authenticated') {
      setIsLoadingAgent(false);
      return;
    }

    setIsLoadingAgent(true);

    try {
      const token = await getAccessToken();
      const apiUrl = import.meta.env.VITE_API_URL || '/api';
      
      const response = await fetch(`${apiUrl}/agent`, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      const data = await response.json();
      setAgentMetadata(data);
      
      // Update document title with agent name
      document.title = data.name ? `${data.name} - Azure AI Agent` : 'Azure AI Agent';
    } catch (error) {
      console.error('Error fetching agent metadata:', error);
      // Fallback data keeps UI functional on error
      setAgentMetadata({
        id: 'fallback-agent',
        object: 'agent',
        createdAt: Date.now() / 1000,
        name: 'Azure AI Agent',
        description: 'Your intelligent conversational partner powered by Azure AI',
        model: 'gpt-4o-mini',
        metadata: { logo: 'Avatar_Default.svg' }
      });
      document.title = 'Azure AI Agent';
    } finally {
      setIsLoadingAgent(false);
    }
  }, [auth.status, getAccessToken]);

  useEffect(() => {
    fetchAgentMetadata();
  }, [fetchAgentMetadata]);

  return (
    <ErrorBoundary>
      {auth.status === 'initializing' || isLoadingAgent ? (
        <div className="app-container app-status-center app-status-column">
          <Spinner size="large" />
          <p className="app-status-message">
            {auth.status === 'initializing' ? 'Preparing your session...' : 'Loading agent...'}
          </p>
        </div>
      ) : (
        <>
          <AuthenticatedTemplate>
            {agentMetadata && (
              <div className="app-container">
                <AgentPreview 
                  agentId={agentMetadata.id}
                  agentName={agentMetadata.name}
                  agentDescription={agentMetadata.description || undefined}
                  agentLogo={agentMetadata.metadata?.logo}
                  starterPrompts={agentMetadata.starterPrompts || undefined}
                />
              </div>
            )}
          </AuthenticatedTemplate>
          <UnauthenticatedTemplate>
            <div className="app-container app-status-center">
              <div className="auth-status-panel">
                <h2 className="auth-status-title">{authErrorMessage.title}</h2>
                <p className="auth-status-text">{authErrorMessage.description} {authErrorMessage.hint}</p>

                {isRedirectingForAuth ? (
                  <p className="auth-status-redirecting">Redirecting to sign in…</p>
                ) : (
                  <Button appearance="primary" onClick={signInAgain}>
                    Sign in again
                  </Button>
                )}
              </div>
            </div>
          </UnauthenticatedTemplate>
        </>
      )}
    </ErrorBoundary>
  );
}

export default App;
