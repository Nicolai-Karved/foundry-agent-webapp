import React from 'react';
import { Label, Select, makeStyles, tokens } from '@fluentui/react-components';
import { useAppState } from '../../hooks/useAppState';
import { useAppContext } from '../../contexts/AppContext';
import type { AgentRouteOverride } from '../../types/standards';

const useStyles = makeStyles({
  panel: {
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: '14px',
    backgroundColor: tokens.colorNeutralBackground1,
    overflow: 'hidden',
    boxShadow: tokens.shadow8,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
  },
  hint: {
    fontSize: tokens.fontSizeBase100,
    color: tokens.colorNeutralForeground3,
  },
});

export const RoutingModePanel: React.FC = () => {
  const styles = useStyles();
  const { settings } = useAppState();
  const { dispatch } = useAppContext();

  return (
    <section className={styles.panel} aria-label="Routing mode">
      <Label htmlFor="agent-route-override">Routing mode</Label>
      <Select
        id="agent-route-override"
        value={settings.agentRouteOverride}
        onChange={(event) => {
          const value = event.target.value as AgentRouteOverride;
          dispatch({ type: 'SET_AGENT_ROUTE_OVERRIDE', agentRouteOverride: value });
        }}
      >
        <option value="auto">Auto-detect</option>
        <option value="air">Force AIR agent</option>
        <option value="eir">Force EIR agent</option>
        <option value="bep">Force BEP agent</option>
      </Select>
      <div className={styles.hint}>
        Auto-detect uses filename, selected standards, and prompt keywords.
      </div>
    </section>
  );
};