import type { DailySummary, SortColumn, SortState } from '../api/types';
import { formatPrice, formatVolume } from './formatting';

interface DailySummaryTableProps {
  /** The days to show, already in the order they should appear. */
  readonly days: readonly DailySummary[];
  readonly sort: SortState;
  readonly onSortChange: (column: SortColumn) => void;
}

/** The columns, in display order, with the heading each one shows. */
const columns: readonly { key: SortColumn; label: string; numeric: boolean }[] = [
  { key: 'day', label: 'Day', numeric: false },
  { key: 'lowAverage', label: 'Low Avg', numeric: true },
  { key: 'highAverage', label: 'High Avg', numeric: true },
  { key: 'volume', label: 'Volume', numeric: true },
];

/**
 * The results table.
 *
 * Presentation only: the parent decides what order the rows are in, so the same ordering can be
 * applied to an export without the two drifting apart. The header stays visible when there are no
 * rows, so the shape of the answer is clear before one has been asked for.
 */
export function DailySummaryTable({ days, sort, onSortChange }: DailySummaryTableProps) {
  return (
    <div className="card">
      <table className="summary-table">
        <thead>
          <tr>
            {columns.map((column) => {
              const active = sort.column === column.key;

              return (
                <th
                  key={column.key}
                  scope="col"
                  className={column.numeric ? 'numeric' : undefined}
                  // Announces the current ordering to screen readers, which otherwise have no way
                  // to know the arrow glyph means anything.
                  aria-sort={active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none'}
                >
                  <button
                    type="button"
                    className={active ? 'sort-button sort-active' : 'sort-button'}
                    onClick={() => onSortChange(column.key)}
                  >
                    {column.label}
                    <span className="sort-arrow" aria-hidden="true">
                      {active ? (sort.direction === 'asc' ? '↑' : '↓') : '↕'}
                    </span>
                  </button>
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {days.map((day) => (
            <tr key={day.day}>
              <td className="day">{day.day}</td>
              <td className="numeric">{formatPrice(day.lowAverage)}</td>
              <td className="numeric">{formatPrice(day.highAverage)}</td>
              <td className="numeric">{formatVolume(day.volume)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
