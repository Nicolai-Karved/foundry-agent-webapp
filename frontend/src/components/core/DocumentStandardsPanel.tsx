import React, { useMemo, useState, useEffect } from 'react';
import { Button, makeStyles, tokens } from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronRight20Regular } from '@fluentui/react-icons';
import { StandardsSelector } from './StandardsSelector';
import { useAppState } from '../../hooks/useAppState';

const useStyles = makeStyles({
  panel: {
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    borderRadius: '14px',
    backgroundColor: tokens.colorNeutralBackground1,
    overflow: 'hidden',
    boxShadow: tokens.shadow8,
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  title: {
    fontSize: tokens.fontSizeBase300,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground1,
  },
  summary: {
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  summaryItem: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  },
  body: {
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalS}`,
  },
  empty: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
});

interface DocumentStandardsPanelProps {
  collapseWhenViewingDocument?: boolean;
}

export const DocumentStandardsPanel: React.FC<DocumentStandardsPanelProps> = ({
  collapseWhenViewingDocument = false,
}) => {
  const styles = useStyles();
  const { settings } = useAppState();
  const [expanded, setExpanded] = useState(true);

  useEffect(() => {
    if (collapseWhenViewingDocument) {
      setExpanded(false);
    }
  }, [collapseWhenViewingDocument]);

  const selected = settings.selectedStandards;

  const selectedSummary = useMemo(() => {
    return selected.map((s) => ({
      id: s.standardId,
      tooltip: [
        s.title ?? '-',
        s.publicationDate ?? '-',
        s.issuingOrganization ?? '-',
      ].join('\n'),
    }));
  }, [selected]);

  return (
    <section className={styles.panel} aria-label="Applicable standards">
      <div className={styles.header}>
        <div className={styles.title}>Applicable standards</div>
        <Button
          appearance="subtle"
          size="small"
          icon={expanded ? <ChevronDown20Regular /> : <ChevronRight20Regular />}
          onClick={() => setExpanded((prev) => !prev)}
          aria-label={expanded ? 'Collapse standards selector' : 'Expand standards selector'}
        >
          {expanded ? 'Collapse' : 'Expand'}
        </Button>
      </div>

      {!expanded && (
        <div className={styles.summary}>
          {selectedSummary.length > 0 ? (
            selectedSummary.map((item) => (
              <div key={item.id} className={styles.summaryItem} title={item.tooltip}>
                {item.id}
              </div>
            ))
          ) : (
            <div className={styles.empty}>No standards selected.</div>
          )}
        </div>
      )}

      {expanded && (
        <div className={styles.body}>
          <StandardsSelector />
        </div>
      )}
    </section>
  );
};
