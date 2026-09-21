import type { DailySummary } from '../api/types';
import { formatPrice } from './formatting';

/**
 * Serialises a result in the format the assessment specifies.
 *
 * Built as text rather than with `JSON.stringify`, because JavaScript numbers carry no scale:
 * stringifying 345.2200 yields `345.22`, losing the trailing zeros that are part of the published
 * precision. Writing the formatted figure into the JSON directly keeps the exported file identical
 * to the API's own response, and keeps the values as JSON numbers rather than quoted strings.
 */
export function toJson(days: readonly DailySummary[]): string {
  const entries = days.map(
    (day) =>
      `  {\n` +
      `    "day": "${day.day}",\n` +
      `    "lowAverage": ${formatPrice(day.lowAverage)},\n` +
      `    "highAverage": ${formatPrice(day.highAverage)},\n` +
      `    "volume": ${day.volume}\n` +
      `  }`,
  );

  return `[\n${entries.join(',\n')}\n]\n`;
}

/**
 * Hands the JSON to the browser as a download named after the symbol.
 *
 * The rows arrive in the order the table is showing them, so an export always matches the screen.
 */
export function downloadJson(symbol: string, days: readonly DailySummary[]): void {
  const blob = new Blob([toJson(days)], { type: 'application/json;charset=utf-8' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.download = `${symbol.toLowerCase()}-daily.json`;
  link.click();

  // The object URL holds the blob in memory until it is released.
  URL.revokeObjectURL(url);
}
