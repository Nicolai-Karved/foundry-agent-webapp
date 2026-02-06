import type { StructuredTask } from '../types/chat';
import styles from './TaskPanel.module.css';

interface TaskPanelProps {
  tasks: StructuredTask[];
  title?: string;
  subtitle?: string;
}

const taskKeyOrder = [
  'id',
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
    if (!taskKeyOrder.includes(key)) {
      ordered.push([key, taskObject[key]]);
    }
  });

  return ordered;
};

export const TaskPanel: React.FC<TaskPanelProps> = ({
  tasks,
  title = 'Tasks',
  subtitle,
}) => {
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
          {tasks.map((task, index) => (
            <div key={`task-${index}`} className={styles.taskCard}>
              {getTaskEntries(task).map(([key, value]) => (
                <div key={`${key}-${index}`} className={styles.field}>
                  <div className={styles.label}>{formatKeyLabel(key)}</div>
                  {renderValue(value)}
                </div>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
