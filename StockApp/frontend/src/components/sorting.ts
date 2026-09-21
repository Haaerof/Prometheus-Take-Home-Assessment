import type { DailySummary, SortColumn, SortState } from '../api/types';

/** The order the table opens in: most recent trading day first. */
export const defaultSort: SortState = { column: 'day', direction: 'desc' };

/**
 * Returns a copy of <paramref name="days"/> in the requested order.
 *
 * Days are compared as strings, which is safe because `yyyy-MM-dd` sorts chronologically; the other
 * three columns are numbers. The input is never mutated, so the API's own ordering stays intact.
 */
export function sortDays(days: readonly DailySummary[], sort: SortState): DailySummary[] {
  const direction = sort.direction === 'asc' ? 1 : -1;

  return [...days].sort((left, right) => direction * compare(left, right, sort.column));
}

/**
 * Decides the order after a header is clicked.
 *
 * A new column starts descending — the newest day, the largest volume — because that is the answer
 * people usually want first. Clicking the column already in use reverses it.
 */
export function nextSort(current: SortState, column: SortColumn): SortState {
  if (current.column !== column) {
    return { column, direction: 'desc' };
  }

  return { column, direction: current.direction === 'desc' ? 'asc' : 'desc' };
}

function compare(left: DailySummary, right: DailySummary, column: SortColumn): number {
  if (column === 'day') {
    return left.day.localeCompare(right.day);
  }

  return left[column] - right[column];
}
