import { useEffect, useMemo, useRef, useState } from 'react';
import type { StructuredTask } from '../types/chat';
import styles from './TaskPanel.module.css';

interface TaskPanelProps {
  tasks: StructuredTask[];
  title?: string;
  subtitle?: string;
  selectedTaskId?: string | null;
  onSelectTask?: (task: StructuredTask, reference?: string | string[], fallbackReference?: string | string[], severity?: string) => void;
}

const taskKeyOrder = [
  'name',
  'severity',
  'description',
  'citation_document_name',
  'citation',
  'document_reference',
  'reference',
  'remediation',
];

const visibleTaskKeys = new Set([
  'description',
  'citation_document_name',
  'citation',
  'document_reference',
  'reference',
  'remediation',
]);

const normalizeComparableText = (value: unknown): string => {
  if (value === null || value === undefined) return '';

  if (Array.isArray(value)) {
    return value.map((entry) => String(entry).trim()).join('\n').toLowerCase();
  }

  if (typeof value === 'object') {
    return JSON.stringify(value).toLowerCase();
  }

  return String(value)
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
};

const formatKeyLabel = (key: string) =>
  key.replace(/_/g, ' ').replace(/\b\w/g, (char) => char.toUpperCase());

const renderValue = (value: unknown) => {
  if (value === null || value === undefined) {
    return <span className={styles.valueMuted}>-</span>;
  }

  if (Array.isArray(value)) {
    return (
      <pre className={styles.valueBlock}>
        {value.map((entry) => String(entry)).join('\n')}
      </pre>
    );
  }

  if (typeof value === 'object') {
    return (
      <pre className={styles.valueBlock}>
        {JSON.stringify(value, null, 2)}
      </pre>
    );
  }

  return <span className={styles.value}>{String(value)}</span>;
};

const getTaskEntries = (task: StructuredTask) => {
  const taskObject = (task && typeof task === 'object') ? task as Record<string, unknown> : {};
  const ordered: Array<[string, unknown]> = [];

  const citationDocumentName = taskObject.citation_document_name;
  const standardId = taskObject.standard_id;
  const shouldPreferStandardId =
    typeof citationDocumentName === 'string' &&
    citationDocumentName.length > 80 &&
    typeof standardId === 'string' &&
    standardId.trim().length > 0;

  const normalizedSeen = new Set<string>();

  taskKeyOrder.forEach((key) => {
    if (!visibleTaskKeys.has(key)) {
      return;
    }

    if (!Object.prototype.hasOwnProperty.call(taskObject, key)) {
      return;
    }

    let value = taskObject[key];

    if (key === 'citation_document_name' && shouldPreferStandardId) {
      value = standardId;
    }

    const normalizedValue = normalizeComparableText(value);
    if (!normalizedValue || normalizedValue === 'n/a' || normalizedValue === 'na') {
      return;
    }

    if (normalizedSeen.has(normalizedValue)) {
      return;
    }

    normalizedSeen.add(normalizedValue);
    ordered.push([key, value]);
  });

  Object.keys(taskObject).forEach((key) => {
    if (!taskKeyOrder.includes(key) && key !== 'id' && visibleTaskKeys.has(key)) {
      const value = taskObject[key];
      const normalizedValue = normalizeComparableText(value);
      if (!normalizedValue || normalizedValue === 'n/a' || normalizedValue === 'na') {
        return;
      }

      if (normalizedSeen.has(normalizedValue)) {
        return;
      }

      normalizedSeen.add(normalizedValue);
      ordered.push([key, value]);
    }
  });

  return ordered;
};

const getTaskId = (task: StructuredTask, index: number) => {
  if (task && typeof task === 'object') {
    const taskObject = task as Record<string, unknown>;
    if (typeof taskObject.id === 'string') return taskObject.id;
    if (typeof taskObject.name === 'string') return taskObject.name;
  }
  return `task-${index}`;
};

const getReference = (task: StructuredTask): string | string[] | undefined => {
  if (!task || typeof task !== 'object') return undefined;
  const taskObject = task as Record<string, unknown>;
  if (typeof taskObject.reference === 'string') return taskObject.reference;
  if (Array.isArray(taskObject.reference)) {
    const entries = taskObject.reference.filter((entry) => typeof entry === 'string');
    if (entries.length > 0) return entries as string[];
  }
  if (typeof taskObject.document_reference === 'string') return taskObject.document_reference;
  return undefined;
};

const getDocumentReference = (task: StructuredTask): string | string[] | undefined => {
  if (!task || typeof task !== 'object') return undefined;
  const taskObject = task as Record<string, unknown>;
  if (typeof taskObject.document_reference === 'string') return taskObject.document_reference;
  if (Array.isArray(taskObject.document_reference)) {
    const entries = taskObject.document_reference.filter((entry) => typeof entry === 'string');
    if (entries.length > 0) return entries as string[];
  }
  return undefined;
};

const getTaskName = (task: StructuredTask): string => {
  if (!task || typeof task !== 'object') return 'Task';
  const taskObject = task as Record<string, unknown>;
  if (typeof taskObject.name === 'string') return taskObject.name;
  return 'Task';
};

const getSeverity = (task: StructuredTask): string => {
  if (!task || typeof task !== 'object') return 'Unknown';
  const taskObject = task as Record<string, unknown>;
  if (typeof taskObject.severity === 'string') return taskObject.severity;
  return 'Unknown';
};

const getSeverityClass = (severity: string) => {
  const normalized = severity.toLowerCase();
  if (normalized === 'critical' || normalized === 'high') return styles.severityCritical;
  if (normalized === 'major' || normalized === 'medium') return styles.severityMajor;
  if (normalized === 'minor' || normalized === 'low') return styles.severityMinor;
  if (normalized === 'info' || normalized === 'informational') return styles.severityInfo;
  return styles.severityNeutral;
};

type TaskResolution = 'open' | 'done' | 'rejected';

const severityRank = (severity: string): number => {
  const normalized = severity.toLowerCase();
  if (normalized === 'critical' || normalized === 'high') return 0;
  if (normalized === 'major') return 1;
  if (normalized === 'medium') return 2;
  if (normalized === 'minor' || normalized === 'low') return 3;
  if (normalized === 'info' || normalized === 'informational') return 4;
  return 5;
};

const resolutionRank = (resolution: TaskResolution): number => {
  if (resolution === 'open') return 0;
  if (resolution === 'done') return 1;
  return 2;
};

export const TaskPanel: React.FC<TaskPanelProps> = ({
  tasks,
  title = 'Tasks',
  subtitle,
  selectedTaskId,
  onSelectTask,
}) => {
  const taskRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const [taskResolutionById, setTaskResolutionById] = useState<Record<string, TaskResolution>>({});

  useEffect(() => {
    setTaskResolutionById((previous) => {
      const nextState: Record<string, TaskResolution> = {};
      tasks.forEach((task, index) => {
        const id = getTaskId(task, index);
        nextState[id] = previous[id] ?? 'open';
      });

      const prevKeys = Object.keys(previous);
      const nextKeys = Object.keys(nextState);
      const isSame =
        prevKeys.length === nextKeys.length
        && nextKeys.every((key) => previous[key] === nextState[key]);

      return isSame ? previous : nextState;
    });
  }, [tasks]);

  useEffect(() => {
    if (!selectedTaskId) return;
    const node = taskRefs.current.get(selectedTaskId);
    if (node) {
      node.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, [selectedTaskId]);

  const sortedTasks = useMemo(() => {
    return tasks
      .map((task, index) => ({ task, index, id: getTaskId(task, index) }))
      .sort((left, right) => {
        const leftResolution = taskResolutionById[left.id] ?? 'open';
        const rightResolution = taskResolutionById[right.id] ?? 'open';

        const byResolution = resolutionRank(leftResolution) - resolutionRank(rightResolution);
        if (byResolution !== 0) {
          return byResolution;
        }

        const bySeverity = severityRank(getSeverity(left.task)) - severityRank(getSeverity(right.task));
        if (bySeverity !== 0) {
          return bySeverity;
        }

        return left.index - right.index;
      });
  }, [tasks, taskResolutionById]);

  const taskCards = useMemo(() => {
    return sortedTasks.map(({ task, id: taskId }) => {
      const isSelected = selectedTaskId === taskId;
      const name = getTaskName(task);
      const severity = getSeverity(task);
      const severityClass = getSeverityClass(severity);
      const entries = getTaskEntries(task).filter(([key]) => key !== 'name' && key !== 'severity');
      const resolution = taskResolutionById[taskId] ?? 'open';
      const isResolved = resolution !== 'open';

      const markTask = (nextResolution: TaskResolution) => {
        setTaskResolutionById((previous) => ({
          ...previous,
          [taskId]: nextResolution,
        }));
      };

      return (
        <div
          key={taskId}
          ref={(node) => {
            if (node) {
              taskRefs.current.set(taskId, node);
            }
          }}
          className={`${styles.taskCard} ${isSelected ? styles.taskCardActive : ''} ${isResolved ? styles.taskCardResolved : ''}`}
        >
          <button
            type="button"
            className={styles.taskHeader}
            onClick={() => onSelectTask?.(task, getDocumentReference(task), getReference(task), severity)}
          >
            <span className={styles.taskName}>{name}</span>
            <span className={`${styles.severityTag} ${severityClass}`}>{severity}</span>
          </button>
          {isSelected && (
            <div className={styles.taskDetails}>
              <div className={styles.taskActions}>
                <button
                  type="button"
                  className={`${styles.taskActionButton} ${resolution === 'done' ? styles.taskActionButtonActive : ''}`}
                  onClick={() => markTask(resolution === 'done' ? 'open' : 'done')}
                >
                  {resolution === 'done' ? 'Undo done' : 'Mark done'}
                </button>
                <button
                  type="button"
                  className={`${styles.taskActionButton} ${resolution === 'rejected' ? styles.taskActionButtonRejected : ''}`}
                  onClick={() => markTask(resolution === 'rejected' ? 'open' : 'rejected')}
                >
                  {resolution === 'rejected' ? 'Undo rejection' : 'Reject'}
                </button>
              </div>
              {entries.map(([key, value]) => (
                <div key={`${taskId}-${key}`} className={styles.field}>
                  <div className={styles.label}>{formatKeyLabel(key)}</div>
                  {renderValue(value)}
                </div>
              ))}
            </div>
          )}
        </div>
      );
    });
  }, [sortedTasks, selectedTaskId, onSelectTask, taskResolutionById]);

  return (
    <div className={styles.panel}>
      <div className={styles.header}>
        <div className={styles.title}>{title}</div>
        {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
      </div>
      {tasks.length === 0 ? (
        <div className={styles.empty}>No tasks</div>
      ) : (
        <div className={styles.taskList}>
          {taskCards}
        </div>
      )}
    </div>
  );
};
