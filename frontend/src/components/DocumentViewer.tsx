import { useMemo, useEffect, useRef, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/TextLayer.css';
import type { IFileAttachment } from '../types/chat';
import styles from './DocumentViewer.module.css';

interface DocumentViewerProps {
  attachments?: IFileAttachment[];
  highlightText?: string | string[] | null;
  highlightFallbackText?: string | string[] | null;
  highlightSeverity?: string | null;
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

const normalizePdfText = (value: string): string => {
  return value
    .replace(/\u00ad/g, '')
    .replace(/[\u2012\u2013\u2014\u2212]/g, ' ')
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
};

pdfjs.GlobalWorkerOptions.workerSrc =
  'https://unpkg.com/pdfjs-dist@4.8.69/build/pdf.worker.min.mjs';

export const DocumentViewer: React.FC<DocumentViewerProps> = ({
  attachments,
  highlightText,
  highlightFallbackText,
  highlightSeverity,
}) => {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const highlightRef = useRef<HTMLSpanElement | null>(null);
  const pdfViewportRef = useRef<HTMLDivElement | null>(null);
  const pdfContainerRef = useRef<HTMLDivElement | null>(null);
  const [pdfReady, setPdfReady] = useState(false);
  const [pdfLoading, setPdfLoading] = useState(false);
  const [numPages, setNumPages] = useState(0);
  const [pdfError, setPdfError] = useState<string | null>(null);
  const [pageWidth, setPageWidth] = useState<number>();
  const [highlightCount, setHighlightCount] = useState(0);
  const [activeMatchIndex, setActiveMatchIndex] = useState(0);
  const lastReferenceKey = useRef<string>('');

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
    if (!selected?.dataUri || !isPdf) {
      setPdfReady(false);
      setPdfError(null);
      setPdfLoading(false);
      return;
    }

    let cancelled = false;
    setPdfLoading(true);
    setPdfError(null);
    setPdfReady(false);

    const loadPdf = async () => {
      try {
        const response = await fetch(selected.dataUri);
        if (!response.ok) {
          throw new Error('Failed to fetch PDF data.');
        }
        const buffer = await response.arrayBuffer();
        if (cancelled) return;
        const bytes = new Uint8Array(buffer);
        const header = new TextDecoder('utf-8', { fatal: false }).decode(bytes.slice(0, 5));
        if (!header.startsWith('%PDF-')) {
          setPdfError('Preview not available for this file type.');
          setPdfLoading(false);
          return;
        }
        setPdfReady(true);
        setPdfLoading(false);
      } catch (error) {
        if (!cancelled) {
          setPdfError('PDF preview failed to load.');
          setPdfLoading(false);
        }
      }
    };

    loadPdf();

    return () => {
      cancelled = true;
    };
  }, [selected?.dataUri, isPdf]);

  const pdfFile = useMemo(() => {
    if (!pdfReady || !selected?.dataUri) return null;
    return selected.dataUri;
  }, [pdfReady, selected?.dataUri]);

  useEffect(() => {
    if (!pdfViewportRef.current || !isPdf) return;
    const container = pdfViewportRef.current;
    const updateWidth = () => {
      const width = container.clientWidth;
      const next = Math.max(320, width - 24);
      setPageWidth(next);
    };

    updateWidth();
    const observer = new ResizeObserver(updateWidth);
    observer.observe(container);

    return () => observer.disconnect();
  }, [isPdf, selected?.dataUri]);

  useEffect(() => {
    if (!isPdf || !pdfContainerRef.current) return;
    const container = pdfContainerRef.current;
    let observer: MutationObserver | null = null;
    let cancelled = false;

    const severity = highlightSeverity?.toLowerCase();
    const severityClass = severity === 'high'
      ? styles.pdfHighlightHigh
      : severity === 'medium'
        ? styles.pdfHighlightMedium
        : severity === 'low'
          ? styles.pdfHighlightLow
          : styles.pdfHighlightNeutral;
    const allHighlightClasses = [
      styles.pdfTextHighlight,
      styles.pdfHighlightHigh,
      styles.pdfHighlightMedium,
      styles.pdfHighlightLow,
      styles.pdfHighlightNeutral,
    ];

    const normalizeList = (value?: string | string[] | null) => {
      if (!value) return [];
      return Array.isArray(value) ? value : [value];
    };

    const buildTokens = (spans: HTMLSpanElement[]) => {
      const tokens: Array<{ token: string; span: HTMLSpanElement }> = [];
      spans.forEach((span) => {
        const spanText = normalizePdfText(span.textContent ?? '');
        if (!spanText) return;
        spanText.split(' ').filter(Boolean).forEach((token) => {
          tokens.push({ token, span });
        });
      });
      return tokens;
    };

    const findMatchesForReference = (
      searchText: string,
      tokens: Array<{ token: string; span: HTMLSpanElement }>
    ) => {
      const searchTokens = normalizePdfText(searchText).split(' ').filter(Boolean);
      if (searchTokens.length === 0) return false;

      if (tokens.length === 0) return false;

      const windowSize = searchTokens.length;
      let bestStart = -1;
      let bestScore = -1;
      const matchStarts: number[] = [];
      const minScore = Math.min(3, windowSize);
      const minRatio = windowSize <= 6 ? 0.7 : 0.45;
      let lastMatchStart = -windowSize;

      for (let i = 0; i <= tokens.length - windowSize; i += 1) {
        let score = 0;
        for (let j = 0; j < windowSize; j += 1) {
          if (tokens[i + j].token === searchTokens[j]) {
            score += 1;
          }
        }
        if (score > bestScore) {
          bestScore = score;
          bestStart = i;
        }
        if (score >= minScore && score / windowSize >= minRatio) {
          if (i - lastMatchStart >= windowSize / 2) {
            matchStarts.push(i);
            lastMatchStart = i;
          }
        }
      }

      const matchRatio = windowSize > 0 ? bestScore / windowSize : 0;
      if (bestStart >= 0 && bestScore >= minScore && matchRatio >= minRatio && matchStarts.length === 0) {
        matchStarts.push(bestStart);
      }

      return matchStarts.map((start) => ({ start, end: start + windowSize }));
    };

    const applyMatchAtIndex = (
      tokens: Array<{ token: string; span: HTMLSpanElement }>,
      matches: Array<{ start: number; end: number }>,
      index: number
    ) => {
      if (matches.length === 0) return false;
      const match = matches[index];
      let firstMatch: HTMLSpanElement | null = null;
      for (let i = match.start; i < match.end; i += 1) {
        const span = tokens[i]?.span;
        if (!span) continue;
        span.classList.add(styles.pdfTextHighlight, severityClass);
        if (!firstMatch) firstMatch = span;
      }
      if (firstMatch) {
        firstMatch.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
      return true;
    };

    const applyHighlight = () => {
      if (cancelled) return false;
      const spans = Array.from(container.querySelectorAll('.react-pdf__Page__textContent span')) as HTMLSpanElement[];
      spans.forEach((span) => {
        span.classList.add(styles.pdfTextSpan);
        allHighlightClasses.forEach((className) => span.classList.remove(className));
      });

      if (spans.length === 0) return false;

      const primaryRefs = normalizeList(highlightText);
      const fallbackRefs = normalizeList(highlightFallbackText);
      const matches: Array<{ start: number; end: number }> = [];

      const tokens = buildTokens(spans);
      if (tokens.length === 0) return false;

      const referenceKey = JSON.stringify({ primaryRefs, fallbackRefs });
      if (referenceKey !== lastReferenceKey.current) {
        lastReferenceKey.current = referenceKey;
        if (activeMatchIndex !== 0) {
          setActiveMatchIndex(0);
        }
      }

      primaryRefs.forEach((reference) => {
        const result = findMatchesForReference(reference, tokens);
        if (result) {
          matches.push(...result);
        }
      });

      if (matches.length === 0) {
        fallbackRefs.forEach((reference) => {
          const result = findMatchesForReference(reference, tokens);
          if (result) {
            matches.push(...result);
          }
        });
      }

      setHighlightCount(matches.length);
      if (matches.length === 0) return !highlightText && !highlightFallbackText;

      const nextIndex = Math.min(activeMatchIndex, matches.length - 1);
      if (nextIndex !== activeMatchIndex) {
        setActiveMatchIndex(nextIndex);
      }

      return applyMatchAtIndex(tokens, matches, nextIndex);
    };

    const applied = applyHighlight();
    if (!applied) {
      observer = new MutationObserver(() => {
        if (applyHighlight()) {
          observer?.disconnect();
        }
      });
      observer.observe(container, { childList: true, subtree: true });
    }

    return () => {
      cancelled = true;
      observer?.disconnect();
    };
  }, [highlightText, highlightFallbackText, highlightSeverity, isPdf, numPages, selected?.dataUri, activeMatchIndex]);

  const handlePrevHighlight = () => {
    if (highlightCount === 0) return;
    setActiveMatchIndex((prev) => (prev - 1 + highlightCount) % highlightCount);
  };

  const handleNextHighlight = () => {
    if (highlightCount === 0) return;
    setActiveMatchIndex((prev) => (prev + 1) % highlightCount);
  };

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
        <div className={styles.highlightControls}>
          <button
            type="button"
            className={styles.highlightButton}
            onClick={handlePrevHighlight}
            disabled={highlightCount === 0}
            aria-label="Previous highlight"
          >
            ↑
          </button>
          <div className={styles.highlightCount}>
            {highlightCount > 0 ? `${activeMatchIndex + 1}/${highlightCount}` : '0/0'}
          </div>
          <button
            type="button"
            className={styles.highlightButton}
            onClick={handleNextHighlight}
            disabled={highlightCount === 0}
            aria-label="Next highlight"
          >
            ↓
          </button>
        </div>
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
          <div ref={pdfViewportRef} className={styles.pdfContainer}>
            {pdfLoading && <div className={styles.empty}>Loading PDF...</div>}
            {pdfError && <div className={styles.empty}>{pdfError}</div>}
            {pdfFile && (
              <Document
                file={pdfFile}
                onLoadSuccess={({ numPages: nextNumPages }) => {
                  setNumPages(nextNumPages);
                  setPdfError(null);
                }}
                onLoadError={() => setPdfError('PDF preview failed to load.')}
                className={styles.pdfDocument}
              >
                <div ref={pdfContainerRef} className={styles.pdfPages}>
                  {Array.from({ length: numPages }, (_, index) => (
                    <div key={`page-${index + 1}`} className={styles.pdfPage}>
                      <Page
                        pageNumber={index + 1}
                        renderTextLayer
                        renderAnnotationLayer={false}
                        className={styles.pdfPageContent}
                        width={pageWidth}
                      />
                    </div>
                  ))}
                </div>
              </Document>
            )}
          </div>
        )}
        {selected && isImage && selected.dataUri && (
          <img
            className={styles.image}
            src={selected.dataUri}
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
        {selected && isPdf && !pdfFile && (
          <div className={styles.empty}>Preparing PDF preview...</div>
        )}
      </div>
    </div>
  );
};
