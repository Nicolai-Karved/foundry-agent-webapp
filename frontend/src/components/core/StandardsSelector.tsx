import React, { useEffect, useMemo, useState } from 'react';
import { Checkbox, Button, makeStyles, tokens } from '@fluentui/react-components';
import { useAppContext } from '../../contexts/AppContext';
import type { StandardSelection } from '../../types/standards';
import { STANDARDS_OPTIONS } from '../../config/standards';

const useStyles = makeStyles({
  container: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
  },
  item: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
    padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusMedium,
    border: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  meta: {
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
  },
  actions: {
    display: 'flex',
    gap: tokens.spacingHorizontalS,
  },
});

export const StandardsSelector: React.FC = () => {
  const styles = useStyles();
  const { state, dispatch } = useAppContext();
  const selected = state.settings.selectedStandards;
  const [options, setOptions] = useState<StandardSelection[]>(STANDARDS_OPTIONS);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;

    const loadStandards = async () => {
      setLoading(true);
      setError(null);

      try {
        const response = await fetch('/api/standards', {
          method: 'GET',
        });

        if (!response.ok) {
          throw new Error(`Failed to load standards (${response.status})`);
        }

        const data = (await response.json()) as StandardSelection[];
        if (!mounted) return;

        if (data.length > 0) {
          setOptions(data);
        } else {
          setOptions(STANDARDS_OPTIONS);
          setError('Standards catalog is empty. Showing built-in defaults.');
        }
      } catch (err) {
        if (!mounted) return;
        setOptions(STANDARDS_OPTIONS);
        setError(`Could not load standards catalog. Showing built-in defaults (${err instanceof Error ? err.message : 'unknown error'}).`);
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };

    loadStandards();
    return () => {
      mounted = false;
    };
  }, []);

  const selectedIds = useMemo(() => new Set(selected.map((s) => s.standardId)), [selected]);

  const toggleStandard = (standard: StandardSelection, checked: boolean) => {
    const next = checked
      ? [...selected, standard]
      : selected.filter((s) => s.standardId !== standard.standardId);
    dispatch({ type: 'SET_STANDARDS_SELECTION', selectedStandards: next });
  };

  const selectAll = () => {
    dispatch({ type: 'SET_STANDARDS_SELECTION', selectedStandards: options });
  };

  const clearAll = () => {
    dispatch({ type: 'SET_STANDARDS_SELECTION', selectedStandards: [] });
  };

  return (
    <div className={styles.container}>
      <div className={styles.actions}>
        <Button appearance="secondary" size="small" onClick={selectAll}>Select all</Button>
        <Button appearance="subtle" size="small" onClick={clearAll}>Clear</Button>
      </div>
      {loading && <div className={styles.meta}>Loading standards from index…</div>}
      {error && <div className={styles.meta}>Could not load standards: {error}</div>}
      {!loading && !error && options.length === 0 && (
        <div className={styles.meta}>No standards found in index. Check that documents are indexed into <code>bim-standards-paragraph-index</code>.</div>
      )}
      <div className={styles.list}>
        {options.map((standard) => (
          <div key={standard.standardId} className={styles.item}>
            <Checkbox
              checked={selectedIds.has(standard.standardId)}
              onChange={(_, data) => toggleStandard(standard, Boolean(data.checked))}
              label={standard.standardId}
            />
            <div className={styles.meta}>
              {standard.title ?? standard.standardId}
              {standard.publicationDate ? ` • ${standard.publicationDate}` : ''}
              {standard.issuingOrganization ? ` • ${standard.issuingOrganization}` : ''}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
