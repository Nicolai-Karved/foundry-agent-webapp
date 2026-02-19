import type { IAnnotation } from '../types/chat';

const STANDARD_NUMBER_REGEX = /\b((?:BS\s+)?(?:EN\s+)?(?:ISO|IEC)\s*\d{3,5}(?:-\d+)*(?::\d{4})?)\b/i;

function normalizeStandardNumber(value: string): string {
  return value
    .replace(/\s+/g, ' ')
    .replace(/^(ISO|IEC)(\d)/i, '$1 $2')
    .trim();
}

export function extractStandardNumber(annotation?: IAnnotation): string | null {
  if (!annotation) return null;

  if (annotation.standardNumber && annotation.standardNumber.trim().length > 0) {
    return normalizeStandardNumber(annotation.standardNumber);
  }

  const candidates = [annotation.label, annotation.quote, annotation.url].filter(
    (value): value is string => typeof value === 'string' && value.trim().length > 0
  );

  for (const candidate of candidates) {
    const match = candidate.match(STANDARD_NUMBER_REGEX);
    if (match?.[1]) {
      return normalizeStandardNumber(match[1]);
    }
  }

  return null;
}

export function buildCitationTooltipText(index: number, annotation?: IAnnotation): string {
  const label = annotation?.label?.trim() || `Citation ${index}`;
  const standardNumber = extractStandardNumber(annotation);
  const quote = annotation?.quote?.trim();

  const lines = [label];

  if (standardNumber) {
    lines.push(`standardNumber: ${standardNumber}`);
  }

  if (quote) {
    lines.push('');
    lines.push(`\"${quote.slice(0, 200)}${quote.length > 200 ? '...' : ''}\"`);
  }

  return lines.join('\n');
}
