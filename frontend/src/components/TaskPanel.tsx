import { useEffect, useMemo, useRef } from 'react';
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
];

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

  taskKeyOrder.forEach((key) => {
    if (Object.prototype.hasOwnProperty.call(taskObject, key)) {
      ordered.push([key, taskObject[key]]);
    }
  });

  Object.keys(taskObject).forEach((key) => {
    if (!taskKeyOrder.includes(key) && key !== 'id') {
      ordered.push([key, taskObject[key]]);
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

const getReference = (task: StructuredTask): string | undefined => {
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
  if (normalized === 'high') return styles.severityHigh;
  if (normalized === 'medium') return styles.severityMedium;
  if (normalized === 'low') return styles.severityLow;
  return styles.severityNeutral;
};

export const TaskPanel: React.FC<TaskPanelProps> = ({
  tasks,
  title = 'Tasks',
  subtitle,
  selectedTaskId,
  onSelectTask,
}) => {
  const taskRefs = useRef<Map<string, HTMLDivElement>>(new Map());

  useEffect(() => {
    if (!selectedTaskId) return;
    const node = taskRefs.current.get(selectedTaskId);
    if (node) {
      node.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
  }, [selectedTaskId]);

  const taskCards = useMemo(() => {
    return tasks.map((task, index) => {
      const taskId = getTaskId(task, index);
      const isSelected = selectedTaskId === taskId;
      const name = getTaskName(task);
      const severity = getSeverity(task);
      const severityClass = getSeverityClass(severity);
      const entries = getTaskEntries(task).filter(([key]) => key !== 'name' && key !== 'severity');

      return (
        <button
          key={taskId}
          type="button"
          ref={(node) => {
            if (node) {
              taskRefs.current.set(taskId, node);
            }
          }}
          className={`${styles.taskCard} ${isSelected ? styles.taskCardActive : ''}`}
          onClick={() => onSelectTask?.(task, getReference(task), getDocumentReference(task), severity)}
        >
          <div className={styles.taskHeader}>
            <span className={styles.taskName}>{name}</span>
            <span className={`${styles.severityTag} ${severityClass}`}>{severity}</span>
          </div>
          {isSelected && (
            <div className={styles.taskDetails}>
              {entries.map(([key, value]) => (
                <div key={`${taskId}-${key}`} className={styles.field}>
                  <div className={styles.label}>{formatKeyLabel(key)}</div>
                  {renderValue(value)}
                </div>
              ))}
            </div>
          )}
        </button>
      );
    });
  }, [tasks, selectedTaskId, onSelectTask]);

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
