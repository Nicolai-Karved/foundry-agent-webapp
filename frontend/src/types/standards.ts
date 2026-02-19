export interface StandardSelection {
  standardId: string;
  title?: string;
  version?: string;
  jurisdiction?: string;
  publicationDate?: string;
  issuingOrganization?: string;
  priority: number;
  mandatory: boolean;
}

export type AgentRouteOverride = 'auto' | 'air' | 'eir' | 'bep';
