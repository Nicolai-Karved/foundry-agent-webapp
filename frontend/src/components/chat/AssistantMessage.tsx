import { Suspense, memo, useMemo, useCallback } from 'react';
import { Spinner, Tooltip, Button } from '@fluentui/react-components';
import { CopilotMessage } from '@fluentui-copilot/react-copilot-chat';
import { DocumentRegular, GlobeRegular, FolderRegular, OpenRegular, ArrowDownload24Regular } from '@fluentui/react-icons';
import jsPDF from 'jspdf';
import { Markdown } from '../core/Markdown';
import { AgentIcon } from '../core/AgentIcon';
import { UsageInfo } from './UsageInfo';
import { useFormatTimestamp } from '../../hooks/useFormatTimestamp';
import { parseContentWithCitations } from '../../utils/citationParser';
import { buildCitationTooltipText } from '../../utils/citationTooltip';
import type { IChatItem, IAnnotation } from '../../types/chat';
import styles from './AssistantMessage.module.css';

interface AssistantMessageProps {
  message: IChatItem;
  agentName?: string;
  agentLogo?: string;
  isStreaming?: boolean;
}

const sanitizeFilenamePart = (value: string): string => {
  const cleaned = value
    .replace(/[<>:"/\\|?*\x00-\x1F]/g, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
    .toLowerCase();

  return cleaned.slice(0, 36) || 'report';
};

const formatTimestampForFilename = (date: Date): string => {
  const year = String(date.getFullYear());
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hour = String(date.getHours()).padStart(2, '0');
  const minute = String(date.getMinutes()).padStart(2, '0');

  return `${year}${month}${day}-${hour}${minute}`;
};

const getShortReportTitle = (content: string): string => {
  const headingMatch = content.match(/^##\s+(.+)$/m);
  if (headingMatch?.[1]) {
    return sanitizeFilenamePart(headingMatch[1]);
  }

  const firstLine = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .find((line) => line.length > 0);

  return sanitizeFilenamePart(firstLine ?? 'report');
};

const normalizePdfText = (value: string): string => {
  return value
    .replace(/\u0000/g, '')
    .replace(/[\u2018\u2019]/g, "'")
    .replace(/[\u201C\u201D]/g, '"')
    .replace(/[\u2013\u2014]/g, '-')
    .replace(/\u2026/g, '...')
    .replace(/\u00A0/g, ' ')
    .replace(/\t/g, '    ')
    .replace(/\s+$/gm, '')
    .normalize('NFKC');
};

const stripInlineMarkdown = (line: string): string =>
  line
    .replace(/\[(.*?)\]\((.*?)\)/g, '$1 ($2)')
    .replace(/`([^`]+)`/g, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/\*([^*]+)\*/g, '$1')
    .trim();

const isTableLine = (line: string): boolean => /^\|.*\|\s*$/.test(line.trim());
const isTableSeparator = (line: string): boolean => /^\|[\s:\-\|]+\|\s*$/.test(line.trim());

const parseTableRow = (line: string): string[] =>
  line
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => stripInlineMarkdown(cell));

const downloadReportPdf = (content: string): void => {
  const generatedAt = new Date();
  const shortTitle = getShortReportTitle(content);
  const timestamp = formatTimestampForFilename(generatedAt);
  const fileName = `${shortTitle}-${timestamp}.pdf`;

  const doc = new jsPDF({ unit: 'pt', format: 'a4' });
  const pageWidth = doc.internal.pageSize.getWidth();
  const pageHeight = doc.internal.pageSize.getHeight();
  const margin = 42;
  const usableWidth = pageWidth - margin * 2;
  const usableHeight = pageHeight - margin * 2;

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(14);
  doc.text(shortTitle.replace(/-/g, ' '), margin, margin);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(10);
  doc.text(normalizePdfText(`Generated: ${generatedAt.toLocaleString()}`), margin, margin + 16);

  const reportText = normalizePdfText(content);
  const lines = reportText.split(/\r?\n/);

  const lineHeight = 14;
  const paragraphSpacing = 5;
  const tableCellPadding = 4;
  let y = margin + 34;

  const ensureRoom = (requiredHeight: number) => {
    if (y + requiredHeight > margin + usableHeight) {
      doc.addPage();
      y = margin;
    }
  };

  const writeWrappedText = (
    text: string,
    x: number,
    maxWidth: number,
    options?: { bold?: boolean; size?: number }
  ) => {
    if (!text.trim()) {
      y += paragraphSpacing;
      return;
    }

    doc.setFont('helvetica', options?.bold ? 'bold' : 'normal');
    doc.setFontSize(options?.size ?? 10);

    const wrapped = doc.splitTextToSize(text, maxWidth);
    const requiredHeight = wrapped.length * lineHeight;
    ensureRoom(requiredHeight + paragraphSpacing);

    wrapped.forEach((line: string) => {
      doc.text(line, x, y);
      y += lineHeight;
    });

    y += paragraphSpacing;
  };

  let index = 0;
  while (index < lines.length) {
    const raw = lines[index];
    const line = stripInlineMarkdown(raw);

    if (!line.trim()) {
      y += paragraphSpacing;
      index += 1;
      continue;
    }

    if (/^---+$/.test(line.trim())) {
      ensureRoom(lineHeight);
      doc.setDrawColor(160);
      doc.line(margin, y - 6, margin + usableWidth, y - 6);
      y += paragraphSpacing;
      index += 1;
      continue;
    }

    if (isTableLine(raw)) {
      const tableLines: string[] = [];
      while (index < lines.length && isTableLine(lines[index])) {
        tableLines.push(lines[index]);
        index += 1;
      }

      const rows = tableLines.filter((row) => !isTableSeparator(row)).map(parseTableRow);
      const colCount = Math.max(1, ...rows.map((row) => row.length));
      const colWidth = usableWidth / colCount;

      rows.forEach((row, rowIndex) => {
        const cells = Array.from({ length: colCount }, (_, colIndex) => row[colIndex] ?? '');
        const wrappedCells = cells.map((cell) => doc.splitTextToSize(cell, colWidth - tableCellPadding * 2));
        const maxLines = Math.max(1, ...wrappedCells.map((cellLines: string[]) => cellLines.length));
        const rowHeight = maxLines * 12 + tableCellPadding * 2;

        ensureRoom(rowHeight + 2);

        cells.forEach((_, colIndex) => {
          const x = margin + colIndex * colWidth;
          doc.setDrawColor(190);
          doc.rect(x, y, colWidth, rowHeight);

          doc.setFont('helvetica', rowIndex === 0 ? 'bold' : 'normal');
          doc.setFontSize(9);
          const cellLines = wrappedCells[colIndex];
          let cellY = y + tableCellPadding + 9;
          cellLines.forEach((cellLine: string) => {
            doc.text(cellLine, x + tableCellPadding, cellY);
            cellY += 12;
          });
        });

        y += rowHeight;
      });

      y += paragraphSpacing;
      continue;
    }

    const headingMatch = raw.match(/^(#{1,6})\s+(.*)$/);
    if (headingMatch) {
      const level = headingMatch[1].length;
      const size = level <= 2 ? 13 : level === 3 ? 11 : 10;
      writeWrappedText(stripInlineMarkdown(headingMatch[2]), margin, usableWidth, { bold: true, size });
      index += 1;
      continue;
    }

    const bulletMatch = line.match(/^(?:[-*]|\d+\.)\s+(.+)$/);
    if (bulletMatch) {
      writeWrappedText(`• ${bulletMatch[1]}`, margin + 8, usableWidth - 8, { size: 10 });
      index += 1;
      continue;
    }

    writeWrappedText(line, margin, usableWidth, { size: 10 });
    index += 1;
  }

  doc.save(fileName);
};

function AssistantMessageComponent({ 
  message, 
  agentName = 'AI Assistant',
  agentLogo,
  isStreaming = false,
}: AssistantMessageProps) {
  const displayAgentName = message.more?.agentName ?? agentName;
  const formatTimestamp = useFormatTimestamp();
  const timestamp = message.more?.time ? formatTimestamp(new Date(message.more.time)) : '';

  const isNonEmptyString = (value: unknown): value is string =>
    typeof value === 'string' && value.trim().length > 0;

  const parseHttpUrl = (value: unknown): string | undefined => {
    if (!isNonEmptyString(value)) {
      return undefined;
    }

    try {
      const parsed = new URL(value);
      return parsed.protocol === 'http:' || parsed.protocol === 'https:'
        ? parsed.toString()
        : undefined;
    } catch {
      return undefined;
    }
  };

  const fallbackAnnotations = useMemo<IAnnotation[]>(() => {
    const tasks = message.structured?.tasks;
    if (!Array.isArray(tasks) || tasks.length === 0) {
      return [];
    }

    const deduped = new Map<string, IAnnotation>();

    tasks.forEach((task) => {
      const citationDocumentName = task['citation_document_name'];
      const citation = task['citation'];
      const reference = task['reference'];

      const label = isNonEmptyString(citationDocumentName)
        ? citationDocumentName.trim()
        : isNonEmptyString(citation)
          ? citation.trim()
          : 'Citation';

      const quote = isNonEmptyString(citation) ? citation.trim() : undefined;
      const url = parseHttpUrl(reference);

      if (!quote && !url && !isNonEmptyString(citationDocumentName)) {
        return;
      }

      const annotation: IAnnotation = {
        type: url ? 'uri_citation' : 'file_citation',
        label,
        ...(quote ? { quote } : {}),
        ...(url ? { url } : {}),
      };

      const key = `${annotation.label}|${annotation.quote ?? ''}|${annotation.url ?? ''}`;
      if (!deduped.has(key)) {
        deduped.set(key, annotation);
      }
    });

    return Array.from(deduped.values());
  }, [message.structured?.tasks]);
  
  // Show custom loading indicator when streaming with no content
  const showLoadingDots = isStreaming && !message.content;
  const showRetrieving = isStreaming && message.content.trim() === 'Retrieving response...';
  const hasRealAnnotations = (message.annotations?.length ?? 0) > 0;
  const effectiveAnnotations = hasRealAnnotations
    ? (message.annotations ?? [])
    : fallbackAnnotations;
  const hasAnnotations = effectiveAnnotations.length > 0;
  
  // Parse content with citations for consistent numbering between inline and footnotes
  const parsedContent = useMemo(() => {
    if (!hasRealAnnotations) return null;
    return parseContentWithCitations(message.content, message.annotations);
  }, [message.content, message.annotations, hasRealAnnotations]);

  // Get unique annotations with consistent indices
  // If the parser found citations (inline placeholders), use those
  // Otherwise, fall back to displaying all annotations as footnotes
  const indexedCitations = useMemo(() => {
    if (parsedContent?.citations && parsedContent.citations.length > 0) {
      return parsedContent.citations;
    }
    // No inline placeholders found - display all annotations as numbered footnotes
    // Deduplicate by label+type for fallback case
    if (effectiveAnnotations.length > 0) {
      const seen = new Map<string, { index: number; annotation: IAnnotation; count: number }>();
      effectiveAnnotations.forEach((annotation) => {
        const key = `${annotation.type}:${annotation.label}:${annotation.url || annotation.fileId || ''}`;
        if (seen.has(key)) {
          seen.get(key)!.count++;
        } else {
          seen.set(key, { index: seen.size + 1, annotation, count: 1 });
        }
      });
      return Array.from(seen.values());
    }
    return [];
  }, [parsedContent, effectiveAnnotations]);
  
  // Handle citation click - scroll to footnote or open URL
  const handleCitationClick = useCallback((index: number, annotation?: IAnnotation) => {
    if (annotation?.type === 'uri_citation' && annotation.url) {
      window.open(annotation.url, '_blank', 'noopener,noreferrer');
    } else {
      // Scroll to citation in footnotes
      const citationElement = document.getElementById(`citation-${message.id}-${index}`);
      if (citationElement) {
        citationElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
        citationElement.classList.add(styles.citationHighlight);
        setTimeout(() => {
          citationElement.classList.remove(styles.citationHighlight);
        }, 2000);
      }
    }
  }, [message.id]);
  
  // Build citation elements matching Azure AI Foundry style
  const renderCitation = (annotation: IAnnotation, index: number, count: number = 1) => {
    const getIcon = () => {
      switch (annotation.type) {
        case 'uri_citation':
          return <GlobeRegular className={styles.citationIcon} />;
        case 'file_path':
          return <FolderRegular className={styles.citationIcon} />;
        default:
          return <DocumentRegular className={styles.citationIcon} />;
      }
    };

    const citationNumber = index;
    const baseTooltip = buildCitationTooltipText(citationNumber, annotation);
    const tooltipContent = count > 1
      ? `${baseTooltip}\n\nReferenced ${count} times`
      : baseTooltip;

    const isClickable = annotation.type === 'uri_citation' && annotation.url;

    const handleClick = () => {
      if (isClickable) {
        window.open(annotation.url, '_blank', 'noopener,noreferrer');
      }
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
      if (isClickable && (e.key === 'Enter' || e.key === ' ')) {
        e.preventDefault();
        handleClick();
      }
    };

    // Render citation button matching Azure AI Foundry style
    return (
      <Tooltip
        key={`${annotation.label}-${index}`}
        content={tooltipContent}
        relationship="description"
        withArrow
      >
        <span 
          id={`citation-${message.id}-${citationNumber}`}
          className={`${styles.citation} ${isClickable ? styles.citationClickable : ''}`}
          {...(isClickable
            ? {
                onClick: handleClick,
                onKeyDown: handleKeyDown,
                role: 'link' as const,
                tabIndex: 0,
              }
            : {})}
        >
          <span className={styles.citationNumber}>{citationNumber}</span>
          <span className={styles.citationContent}>
            {getIcon()}
            <span className={styles.citationLabel}>{annotation.label}</span>
            {count > 1 && <span className={styles.citationCount}>×{count}</span>}
            {isClickable && <OpenRegular className={styles.citationExternalIcon} />}
          </span>
        </span>
      </Tooltip>
    );
  };

  const citations = indexedCitations.map(({ index, annotation, count }) => 
    renderCitation(annotation, index, count)
  );

  const canDownloadReport = !isStreaming && message.content.trim().length > 0;

  
  return (
    <CopilotMessage
      id={`msg-${message.id}`}
      avatar={<AgentIcon logoUrl={agentLogo} />}
      name={displayAgentName}
      loadingState="none"
      className={styles.copilotMessage}
      disclaimer={<span>AI-generated content may be incorrect</span>}
      footnote={
        <div className={styles.footnoteContainer}>
          {hasAnnotations && !isStreaming && (
            <div className={styles.citationList}>
              {citations}
            </div>
          )}
          <div className={styles.metadataRow}>
            {timestamp && <span className={styles.timestamp}>{timestamp}</span>}
            {canDownloadReport && (
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => downloadReportPdf(message.content)}
                className={styles.downloadButton}
                aria-label="Download report as PDF"
                title="Download report as PDF"
              >
                PDF
              </Button>
            )}
            {message.more?.usage && (
              <UsageInfo 
                info={message.more.usage} 
                duration={message.duration} 
              />
            )}
          </div>
        </div>
      }
    >
      {showLoadingDots ? (
        <div className={styles.loadingDots}>
          <span></span>
          <span></span>
          <span></span>
        </div>
      ) : showRetrieving ? (
        <div className={styles.retrievingRow}>
          <span className={styles.retrievingText}>Retrieving response</span>
          <span className={styles.retrievingDots}>
            <span></span>
            <span></span>
            <span></span>
          </span>
        </div>
      ) : (
        <Suspense fallback={<Spinner size="small" />}>
          <Markdown 
            content={message.content} 
            annotations={message.annotations}
            onCitationClick={handleCitationClick}
          />
        </Suspense>
      )}
    </CopilotMessage>
  );
}

export const AssistantMessage = memo(AssistantMessageComponent, (prev, next) => {
  // Re-render only if streaming state or content/usage/annotations changes
  return (
    prev.message.id === next.message.id &&
    prev.message.content === next.message.content &&
    prev.isStreaming === next.isStreaming &&
    prev.agentLogo === next.agentLogo &&
    prev.message.more?.agentName === next.message.more?.agentName &&
    prev.message.more?.usage === next.message.more?.usage &&
    prev.message.annotations?.length === next.message.annotations?.length
  );
});
