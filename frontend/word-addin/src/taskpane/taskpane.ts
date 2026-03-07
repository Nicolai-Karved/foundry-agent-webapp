declare const Office: {
  onReady: (callback: () => void) => void;
};

type ComplianceTask = {
  taskId: string;
  documentId: string;
  title: string;
  description: string;
  status: string;
  citation: string;
  referenceSource: string;
  version: number;
  anchor: {
    anchorType: 'contentControlTag' | 'textSearchFallback';
    anchorValue: string;
    confidence: number;
    lastValidatedAt: string;
  };
};

type ComplianceTaskListResponse = {
  documentId: string;
  correlationId: string;
  tasks: ComplianceTask[];
};

type CitationContextResponse = {
  taskId: string;
  documentId: string;
  citation: string;
  referenceSource: string;
  context: string;
};

type ResolvedAnchor = {
  range: any;
  anchorQuality: 'contentControlTag' | 'textSearchFallback' | 'textSearchFallbackLowConfidence' | 'none';
};

type DocumentLocalAnchorOverride = {
  anchorType: 'contentControlTag' | 'textSearchFallback';
  anchorValue: string;
  confidence: number;
  lastValidatedAt: string;
};

type DocumentLocalState = {
  selectedTaskId: string | null;
  statusByTaskId: Record<string, string>;
  anchorByTaskId: Record<string, DocumentLocalAnchorOverride>;
};

type OfficeSettingsHandle = {
  get: (key: string) => unknown;
  set: (key: string, value: unknown) => void;
  saveAsync: (callback?: (result?: unknown) => void) => void;
};

const FALLBACK_CONFIDENCE_THRESHOLD = 0.85;
const COMMENT_SIGNATURE_STORAGE_PREFIX = 'fs0001-comment-signature';
const DOCUMENT_LOCAL_STATE_KEY = 'fs0001-document-local-state-v1';

const commentUpsertSignatureByTaskKey = new Map<string, string>();
let telemetrySinkUnavailable = false;

const taskState = {
  selectedTaskId: null as string | null,
  tasks: [] as ComplianceTask[]
};

function createDefaultDocumentLocalState(): DocumentLocalState {
  return {
    selectedTaskId: null,
    statusByTaskId: {},
    anchorByTaskId: {}
  };
}

function getDocumentSettingsHandle(): OfficeSettingsHandle | null {
  const officeContext = (Office as unknown as {
    context?: {
      document?: {
        settings?: OfficeSettingsHandle;
      };
    };
  }).context;

  return officeContext?.document?.settings ?? null;
}

function readDocumentLocalState(): DocumentLocalState {
  const settings = getDocumentSettingsHandle();
  if (!settings) {
    return createDefaultDocumentLocalState();
  }

  try {
    const rawState = settings.get(DOCUMENT_LOCAL_STATE_KEY);
    if (!rawState || typeof rawState !== 'object') {
      return createDefaultDocumentLocalState();
    }

    const candidate = rawState as Partial<DocumentLocalState>;
    return {
      selectedTaskId: typeof candidate.selectedTaskId === 'string' ? candidate.selectedTaskId : null,
      statusByTaskId: candidate.statusByTaskId ?? {},
      anchorByTaskId: candidate.anchorByTaskId ?? {}
    };
  } catch {
    return createDefaultDocumentLocalState();
  }
}

function saveDocumentLocalState(state: DocumentLocalState): void {
  const settings = getDocumentSettingsHandle();
  if (!settings) {
    return;
  }

  try {
    settings.set(DOCUMENT_LOCAL_STATE_KEY, state);
    settings.saveAsync();
  } catch {
    // Best-effort local persistence only.
  }
}

function setDocumentLocalSelectedTaskId(selectedTaskId: string | null): void {
  const state = readDocumentLocalState();
  state.selectedTaskId = selectedTaskId;
  saveDocumentLocalState(state);
}

function setDocumentLocalTaskStatus(taskId: string, status: string): void {
  const state = readDocumentLocalState();
  state.statusByTaskId[taskId] = status;
  saveDocumentLocalState(state);
}

function setDocumentLocalAnchorOverride(task: ComplianceTask, anchorOverride: DocumentLocalAnchorOverride): void {
  const state = readDocumentLocalState();
  state.anchorByTaskId[task.taskId] = anchorOverride;
  saveDocumentLocalState(state);
}

function applyDocumentLocalOverrides(tasks: ComplianceTask[]): ComplianceTask[] {
  const state = readDocumentLocalState();

  return tasks.map((task) => {
    const statusOverride = state.statusByTaskId[task.taskId];
    const anchorOverride = state.anchorByTaskId[task.taskId];

    return {
      ...task,
      status: statusOverride ?? task.status,
      anchor: anchorOverride
        ? {
            anchorType: anchorOverride.anchorType,
            anchorValue: anchorOverride.anchorValue,
            confidence: anchorOverride.confidence,
            lastValidatedAt: anchorOverride.lastValidatedAt
          }
        : task.anchor
    };
  });
}

function createCorrelationId(): string {
  return crypto.randomUUID();
}

function emitTelemetry(eventName: string, properties: Record<string, unknown>): void {
  const correlationId = (properties.correlationId as string | undefined) ?? createCorrelationId();

  try {
    console.info('[FS-0001 telemetry]', {
      eventName,
      timestamp: new Date().toISOString(),
      correlationId,
      ...properties
    });

    if (!telemetrySinkUnavailable) {
      void sendTelemetryEvent(eventName, {
        ...properties,
        correlationId,
        source: 'word-addin-taskpane'
      }, correlationId);
    }
  } catch {
    // Best-effort telemetry only.
  }
}

async function sendTelemetryEvent(
  eventName: string,
  properties: Record<string, unknown>,
  correlationId: string
): Promise<void> {
  try {
    const payloadProperties = Object.entries(properties).reduce<Record<string, string>>((acc, [key, value]) => {
      acc[key] = value === undefined || value === null ? '' : String(value);
      return acc;
    }, {});

    await fetch('/api/telemetry/events', {
      method: 'POST',
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        'X-Correlation-Id': correlationId
      },
      body: JSON.stringify({
        eventName,
        occurredAtUtc: new Date().toISOString(),
        properties: payloadProperties
      })
    });
  } catch {
    telemetrySinkUnavailable = true;
    console.warn('[FS-0001 telemetry] telemetry sink unavailable; using console fallback only.');
  }
}

async function getJson<T>(url: string, correlationId?: string): Promise<T> {
  const requestCorrelationId = correlationId ?? createCorrelationId();

  const response = await fetch(url, {
    credentials: 'include',
    headers: {
      'X-Correlation-Id': requestCorrelationId
    }
  });

  if (!response.ok) {
    emitTelemetry('api_request_failed', {
      url,
      method: 'GET',
      correlationId: requestCorrelationId,
      status: response.status
    });
    throw new Error(`Request failed: ${response.status}`);
  }

  emitTelemetry('api_request_succeeded', {
    url,
    method: 'GET',
    correlationId: requestCorrelationId,
    status: response.status
  });

  return (await response.json()) as T;
}

async function postJson<T>(url: string, body: unknown, correlationId?: string): Promise<T> {
  const requestCorrelationId = correlationId ?? createCorrelationId();

  const response = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': requestCorrelationId
    },
    body: JSON.stringify(body)
  });

  if (!response.ok && response.status !== 202) {
    emitTelemetry('api_request_failed', {
      url,
      method: 'POST',
      correlationId: requestCorrelationId,
      status: response.status
    });
    throw new Error(`Request failed: ${response.status}`);
  }

  emitTelemetry('api_request_succeeded', {
    url,
    method: 'POST',
    correlationId: requestCorrelationId,
    status: response.status
  });

  return (await response.json()) as T;
}

async function patchJson<T>(url: string, body: unknown, correlationId?: string): Promise<T> {
  const requestCorrelationId = correlationId ?? createCorrelationId();

  const response = await fetch(url, {
    method: 'PATCH',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': requestCorrelationId
    },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    emitTelemetry('api_request_failed', {
      url,
      method: 'PATCH',
      correlationId: requestCorrelationId,
      status: response.status
    });
    throw new Error(`Request failed: ${response.status}`);
  }

  emitTelemetry('api_request_succeeded', {
    url,
    method: 'PATCH',
    correlationId: requestCorrelationId,
    status: response.status
  });

  return (await response.json()) as T;
}

function setStatus(text: string): void {
  const statusElement = document.getElementById('status');
  if (!statusElement) {
    return;
  }

  statusElement.textContent = text;
}

function renderTasks(tasks: ComplianceTask[]): void {
  const taskList = document.getElementById('task-list');
  if (!taskList) {
    return;
  }

  taskList.innerHTML = '';
  taskState.tasks = tasks;

  tasks.forEach((task) => {
    const item = document.createElement('li');

    const label = document.createElement('span');
    label.textContent = `${task.title} (${task.status})`;
    item.appendChild(label);

    const actions = document.createElement('span');
    actions.className = 'task-actions';

    const selectButton = document.createElement('button');
    selectButton.textContent = 'Select';
    selectButton.addEventListener('click', () => {
      void selectTask(task);
    });

    const inReviewButton = document.createElement('button');
    inReviewButton.textContent = 'Set In Review';
    inReviewButton.addEventListener('click', () => {
      void updateTaskStatus(task, 'in_review');
    });

    const doneButton = document.createElement('button');
    doneButton.textContent = 'Set Done';
    doneButton.addEventListener('click', () => {
      void updateTaskStatus(task, 'done');
    });

    const highlightButton = document.createElement('button');
    highlightButton.textContent = 'Highlight';
    highlightButton.addEventListener('click', () => {
      void highlightTaskAnchor(task);
    });

    actions.appendChild(selectButton);
    actions.appendChild(inReviewButton);
    actions.appendChild(doneButton);
    actions.appendChild(highlightButton);
    item.appendChild(actions);

    taskList.appendChild(item);
  });
}

function getDocumentId(): string | null {
  const input = document.getElementById('document-id') as HTMLInputElement | null;
  const documentId = input?.value?.trim();
  return documentId || null;
}

function setCitationContext(text: string): void {
  const citationElement = document.getElementById('citation-context');
  if (!citationElement) {
    return;
  }

  citationElement.textContent = text;
}

function setSelectedTask(task: ComplianceTask | null): void {
  const selectedTaskElement = document.getElementById('selected-task');
  if (!selectedTaskElement) {
    return;
  }

  if (!task) {
    selectedTaskElement.textContent = 'No task selected.';
    return;
  }

  selectedTaskElement.textContent = `Selected: ${task.title} [${task.status}]`;
}

function getWordRuntime(): { run: (batch: (context: any) => Promise<void>) => Promise<void> } | null {
  const wordRuntime = (globalThis as {
    Word?: { run: (batch: (context: any) => Promise<void>) => Promise<void> };
  }).Word;

  if (!wordRuntime) {
    return null;
  }

  return wordRuntime;
}

async function resolveTaskAnchor(context: any, task: ComplianceTask): Promise<ResolvedAnchor> {
  const document = context.document;

  if (task.anchor.anchorType === 'contentControlTag') {
    const controls = document.contentControls.getByTag(task.anchor.anchorValue);
    const firstControl = controls.getFirstOrNullObject();
    firstControl.load('isNullObject');
    await context.sync();

    if (!firstControl.isNullObject) {
      return {
        range: firstControl.getRange(),
        anchorQuality: 'contentControlTag'
      };
    }
  }

  const searchResults = document.body.search(task.anchor.anchorValue, {
    matchCase: false,
    matchWholeWord: false
  });

  searchResults.load('items');
  await context.sync();

  if (searchResults.items.length === 0) {
    return {
      range: null,
      anchorQuality: 'none'
    };
  }

  if (task.anchor.confidence < FALLBACK_CONFIDENCE_THRESHOLD) {
    return {
      range: searchResults.items[0],
      anchorQuality: 'textSearchFallbackLowConfidence'
    };
  }

  return {
    range: searchResults.items[0],
    anchorQuality: 'textSearchFallback'
  };
}

function buildCitationComment(task: ComplianceTask, contextText: string): string {
  return `[FS0001:${task.taskId}] [${task.referenceSource}] ${contextText}`;
}

function getTaskCommentMarker(task: ComplianceTask): string {
  return `[FS0001:${task.taskId}]`;
}

function getCommentStorageKey(task: ComplianceTask): string {
  return `${COMMENT_SIGNATURE_STORAGE_PREFIX}:${task.documentId}:${task.taskId}`;
}

function getStoredCommentSignature(task: ComplianceTask): string | null {
  const key = getCommentStorageKey(task);

  if (commentUpsertSignatureByTaskKey.has(key)) {
    return commentUpsertSignatureByTaskKey.get(key) ?? null;
  }

  try {
    const storedSignature = globalThis.localStorage?.getItem(key) ?? null;
    if (storedSignature) {
      commentUpsertSignatureByTaskKey.set(key, storedSignature);
    }

    return storedSignature;
  } catch {
    return null;
  }
}

function storeCommentSignature(task: ComplianceTask, signature: string): void {
  const key = getCommentStorageKey(task);
  commentUpsertSignatureByTaskKey.set(key, signature);

  try {
    globalThis.localStorage?.setItem(key, signature);
  } catch {
    // Non-fatal: continue with in-memory idempotency only.
  }
}

async function loadTasks(): Promise<void> {
  const documentId = getDocumentId();
  if (!documentId) {
    setStatus('Please provide a document ID.');
    return;
  }

  setStatus('Loading tasks...');
  const correlationId = createCorrelationId();
  const payload = await getJson<ComplianceTaskListResponse>(`/api/tasks?documentId=${encodeURIComponent(documentId)}`, correlationId);
  const tasksWithLocalOverrides = applyDocumentLocalOverrides(payload.tasks ?? []);
  renderTasks(tasksWithLocalOverrides);

  const localState = readDocumentLocalState();
  if (!taskState.selectedTaskId && localState.selectedTaskId) {
    taskState.selectedTaskId = localState.selectedTaskId;
  }

  if (taskState.selectedTaskId) {
    const matchedTask = payload.tasks.find((task) => task.taskId === taskState.selectedTaskId) ?? null;
    setSelectedTask(matchedTask);
  }

  setStatus('Tasks loaded.');
}

async function rerunVerification(): Promise<void> {
  const documentId = getDocumentId();
  if (!documentId) {
    setStatus('Please provide a document ID.');
    return;
  }

  setStatus('Submitting re-verify request...');
  const correlationId = createCorrelationId();
  await postJson('/api/verification/rerun', { documentId, includeSuggestions: true }, correlationId);
  await loadTasks();
}

async function selectTask(task: ComplianceTask): Promise<void> {
  taskState.selectedTaskId = task.taskId;
  setDocumentLocalSelectedTaskId(task.taskId);
  setSelectedTask(task);

  const documentId = getDocumentId();
  if (!documentId) {
    setStatus('Please provide a document ID.');
    return;
  }

  setStatus('Loading citation context...');
  const correlationId = createCorrelationId();
  const citationContext = await getJson<CitationContextResponse>(
    `/api/tasks/${encodeURIComponent(task.taskId)}/citation-context?documentId=${encodeURIComponent(documentId)}`,
    correlationId
  );

  setCitationContext(`${citationContext.referenceSource}: ${citationContext.context}`);
  await highlightTaskAnchor(task);
  await upsertCitationComment(task, citationContext.context);
  setStatus('Task selected.');
}

async function updateTaskStatus(task: ComplianceTask, status: string): Promise<void> {
  const documentId = getDocumentId();
  if (!documentId) {
    setStatus('Please provide a document ID.');
    return;
  }

  setStatus(`Updating status to ${status}...`);
  const correlationId = createCorrelationId();

  const nextTasks = taskState.tasks.map((candidate) =>
    candidate.taskId === task.taskId
      ? {
          ...candidate,
          status,
          version: candidate.version + 1
        }
      : candidate
  );

  taskState.tasks = nextTasks;
  renderTasks(nextTasks);
  setDocumentLocalTaskStatus(task.taskId, status);

  try {
    await patchJson(`/api/tasks/${encodeURIComponent(task.taskId)}/status`, {
      documentId,
      status,
      expectedVersion: task.version
    }, correlationId);
  } catch (error) {
    await loadTasks();
    throw error;
  }

  await loadTasks();
  setStatus('Status updated.');
}

async function highlightTaskAnchor(task: ComplianceTask): Promise<void> {
  const wordRuntime = getWordRuntime();

  if (!wordRuntime) {
    setStatus('Word runtime not available in this host session.');
    return;
  }

  await wordRuntime.run(async (context: any) => {
    const resolved = await resolveTaskAnchor(context, task);
    const resolvedRange = resolved.range;

    if (!resolvedRange) {
      setStatus('Anchor not found in document.');
      return;
    }

    resolvedRange.font.highlightColor = '#FFF59D';
    resolvedRange.select();
    await context.sync();

    if (resolved.anchorQuality === 'textSearchFallbackLowConfidence') {
      setStatus('Anchor highlighted with low-confidence fallback. Please verify before editing.');
      return;
    }

    if (resolved.anchorQuality === 'textSearchFallback') {
      setDocumentLocalAnchorOverride(task, {
        anchorType: 'textSearchFallback',
        anchorValue: task.anchor.anchorValue,
        confidence: task.anchor.confidence,
        lastValidatedAt: new Date().toISOString()
      });
    }

    setStatus(`Anchor highlighted (${resolved.anchorQuality}).`);
  });
}

async function upsertCitationComment(task: ComplianceTask, contextText: string): Promise<void> {
  const wordRuntime = getWordRuntime();

  if (!wordRuntime) {
    return;
  }

  const commentText = buildCitationComment(task, contextText);
  const commentMarker = getTaskCommentMarker(task);
  const commentSignature = `${task.taskId}|${task.anchor.anchorValue}|${commentText}`;
  const previousSignature = getStoredCommentSignature(task);
  if (previousSignature === commentSignature) {
    return;
  }

  await wordRuntime.run(async (context: any) => {
    const resolved = await resolveTaskAnchor(context, task);
    const resolvedRange = resolved.range;

    if (!resolvedRange) {
      return;
    }

    if (resolved.anchorQuality === 'textSearchFallbackLowConfidence') {
      setStatus('Citation comment skipped due to low-confidence fallback anchor.');
      return;
    }

    let didUpsertExistingComment = false;

    try {
      if (typeof context.document.body.getComments === 'function') {
        const comments = context.document.body.getComments();
        comments.load('items');
        await context.sync();

        for (const comment of comments.items) {
          comment.load('content');
        }

        await context.sync();

        const existingTaskComment = comments.items.find(
          (comment: any) => typeof comment.content === 'string' && comment.content.includes(commentMarker)
        );

        if (existingTaskComment) {
          if (existingTaskComment.content !== commentText) {
            if (typeof existingTaskComment.delete === 'function') {
              existingTaskComment.delete();
              await context.sync();
            }

            resolvedRange.insertComment(commentText);
            await context.sync();
          }

          didUpsertExistingComment = true;
        }
      }
    } catch {
      // If comment collection APIs are unavailable in this host, fallback to insert path below.
      didUpsertExistingComment = false;
    }

    if (!didUpsertExistingComment) {
      resolvedRange.insertComment(commentText);
      await context.sync();
    }

    if (resolved.anchorQuality === 'textSearchFallback') {
      setDocumentLocalAnchorOverride(task, {
        anchorType: 'textSearchFallback',
        anchorValue: task.anchor.anchorValue,
        confidence: task.anchor.confidence,
        lastValidatedAt: new Date().toISOString()
      });
    }

    storeCommentSignature(task, commentSignature);
  });
}

Office.onReady(() => {
  const loadTasksButton = document.getElementById('load-tasks');
  const rerunButton = document.getElementById('rerun-verification');

  loadTasksButton?.addEventListener('click', () => {
    void loadTasks();
  });

  rerunButton?.addEventListener('click', () => {
    void rerunVerification();
  });
});
