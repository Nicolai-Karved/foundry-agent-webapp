import { useMemo, useEffect, useRef, useState } from 'react';
import * as pdfjsLib from 'pdfjs-dist/build/pdf';
import pdfWorker from 'pdfjs-dist/build/pdf.worker.mjs?url';
import type { IFileAttachment } from '../types/chat';
import styles from './DocumentViewer.module.css';

interface DocumentViewerProps {
  attachments?: IFileAttachment[];
  highlightText?: string | null;
}

const getMimeFromDataUri = (dataUri?: string): string => {
  if (!dataUri) return '';
  const match = dataUri.match(/^data:([^;]+);/i);
  return match ? match[1].toLowerCase() : '';
};

const decodeBase64 = (base64: string): string => {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return new TextDecoder('utf-8', { fatal: false }).decode(bytes);
};

const extractDataUriPayload = (dataUri?: string): string => {
  if (!dataUri) return '';
  const commaIndex = dataUri.indexOf(',');
  return commaIndex >= 0 ? dataUri.slice(commaIndex + 1) : '';
};

pdfjsLib.GlobalWorkerOptions.workerSrc = pdfWorker;

export const DocumentViewer: React.FC<DocumentViewerProps> = ({
  attachments,
  highlightText,
}) => {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const highlightRef = useRef<HTMLSpanElement | null>(null);
  const pdfContainerRef = useRef<HTMLDivElement | null>(null);

  const [objectUrl, setObjectUrl] = useState<string | null>(null);
  const [pdfLoading, setPdfLoading] = useState(false);
  const [pdfError, setPdfError] = useState<string | null>(null);

  const files = attachments && attachments.length > 0 ? attachments : [];
  const selected = files[selectedIndex];

  const mimeType = getMimeFromDataUri(selected?.dataUri);
  const isPdf = mimeType === 'application/pdf';
  const isImage = mimeType.startsWith('image/');
  const isText = mimeType.startsWith('text/') || ['application/json', 'application/xml'].includes(mimeType);

  const textContent = useMemo(() => {
    if (!selected?.dataUri || !isText) return '';
    const payload = extractDataUriPayload(selected.dataUri);
    if (!payload) return '';
    return decodeBase64(payload);
  }, [selected?.dataUri, isText]);

  useEffect(() => {
    if (!selected?.dataUri || (!isPdf && !isImage)) {
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
        setObjectUrl(null);
      }
      return undefined;
    }

    const payload = extractDataUriPayload(selected.dataUri);
    if (!payload) return undefined;

    const binary = atob(payload);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) {
      bytes[i] = binary.charCodeAt(i);
    }

    const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    setObjectUrl(url);

    return () => {
      URL.revokeObjectURL(url);
    };
  }, [selected?.dataUri, mimeType, isPdf, isImage]);

  useEffect(() => {
    if (!isPdf || !selected?.dataUri || !pdfContainerRef.current) {
      return undefined;
    }

    const container = pdfContainerRef.current;
    container.innerHTML = '';
    setPdfError(null);
    setPdfLoading(true);

    const payload = extractDataUriPayload(selected.dataUri);
    if (!payload) {
      setPdfLoading(false);
      setPdfError('PDF payload not available.');
      return undefined;
    }

    const binary = atob(payload);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i += 1) {
      bytes[i] = binary.charCodeAt(i);
    }

    let cancelled = false;

    const render = async () => {
      try {
        const pdf = await pdfjsLib.getDocument({ data: bytes }).promise;
        for (let pageNum = 1; pageNum <= pdf.numPages; pageNum += 1) {
          if (cancelled) return;
          const page = await pdf.getPage(pageNum);
          const viewport = page.getViewport({ scale: 1.25 });
          const canvas = document.createElement('canvas');
          canvas.className = styles.pdfCanvas;
          canvas.width = viewport.width;
          canvas.height = viewport.height;
          const context = canvas.getContext('2d');
          if (!context) continue;
          const wrapper = document.createElement('div');
          wrapper.className = styles.pdfPage;
          wrapper.appendChild(canvas);
          container.appendChild(wrapper);
          await page.render({ canvasContext: context, viewport }).promise;
        }
        if (!cancelled) setPdfLoading(false);
      } catch (error) {
        if (!cancelled) {
          setPdfLoading(false);
          setPdfError('PDF preview failed to load.');
        }
      }
    };

    render();

    return () => {
      cancelled = true;
    };
  }, [isPdf, selected?.dataUri]);

  const highlightedContent = useMemo(() => {
    if (!textContent) return { before: textContent, match: '', after: '' };
    if (!highlightText) return { before: textContent, match: '', after: '' };
    const lower = textContent.toLowerCase();
    const search = highlightText.toLowerCase();
    const index = lower.indexOf(search);
    if (index < 0) return { before: textContent, match: '', after: '' };
    return {
      before: textContent.slice(0, index),
      match: textContent.slice(index, index + search.length),
      after: textContent.slice(index + search.length),
    };
  }, [textContent, highlightText]);

  useEffect(() => {
    if (highlightRef.current) {
      highlightRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [highlightedContent.match]);

  return (
    <div className={styles.viewer}>
      <div className={styles.header}>
        <div className={styles.title}>Document</div>
        {files.length > 1 && (
          <div className={styles.fileTabs}>
            {files.map((file, index) => (
              <button
                key={`${file.fileName}-${index}`}
                className={`${styles.fileTab} ${index === selectedIndex ? styles.fileTabActive : ''}`}
                type="button"
                onClick={() => setSelectedIndex(index)}
              >
                {file.fileName}
              </button>
            ))}
          </div>
        )}
        {selected && files.length <= 1 && (
          <div className={styles.fileName}>{selected.fileName}</div>
        )}
      </div>

      <div className={styles.content}>
        {!selected && (
          <div className={styles.empty}>Upload a document to view it here.</div>
        )}
        {selected && isPdf && (
          <div className={styles.pdfContainer}>
            {pdfLoading && <div className={styles.empty}>Loading PDF...</div>}
            {pdfError && <div className={styles.empty}>{pdfError}</div>}
            <div ref={pdfContainerRef} className={styles.pdfPages} />
          </div>
        )}
        {selected && isImage && objectUrl && (
          <img
            className={styles.image}
            src={objectUrl}
            alt={selected.fileName}
          />
        )}
        {selected && isText && (
          <pre className={styles.textContent}>
            {highlightedContent.before}
            {highlightedContent.match && (
              <span ref={highlightRef} className={styles.highlight}>
                {highlightedContent.match}
              </span>
            )}
            {highlightedContent.after}
          </pre>
        )}
        {selected && !isPdf && !isImage && !isText && (
          <div className={styles.empty}>Preview not available for this file type.</div>
        )}
        {selected && isPdf && !objectUrl && (
          <div className={styles.empty}>Preparing PDF preview...</div>
        )}
      </div>
    </div>
  );
};
