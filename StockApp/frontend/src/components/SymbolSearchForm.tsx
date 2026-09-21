import { useState, type FormEvent } from 'react';
import type { RoundingMode } from '../api/types';

interface SymbolSearchFormProps {
  /** Disables input while a request is in flight. */
  readonly busy: boolean;
  readonly rounding: RoundingMode;
  readonly onRoundingChange: (rounding: RoundingMode) => void;
  readonly onSearch: (symbol: string) => void;
  /** Exports the current results. Absent when there is nothing to export. */
  readonly onExport?: () => void;
}

/** The rounding strategies offered in the UI, in the order the wireframe shows them. */
const roundingModes: readonly { value: RoundingMode; hint: string }[] = [
  { value: 'AwayFromZero', hint: 'Round halves up (default)' },
  { value: 'ToZero', hint: 'Truncate: cut off rather than round' },
];

/**
 * The control row: rounding strategy, symbol entry, search, and export.
 *
 * The symbol is held locally and only raised on submit — the parent owns results, not keystrokes.
 */
export function SymbolSearchForm({
  busy,
  rounding,
  onRoundingChange,
  onSearch,
  onExport,
}: SymbolSearchFormProps) {
  const [symbol, setSymbol] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = symbol.trim();
    if (trimmed) {
      onSearch(trimmed);
    }
  }

  return (
    <form className="controls" onSubmit={handleSubmit}>
      <div className="segmented" role="group" aria-label="Rounding strategy">
        {roundingModes.map((mode) => (
          <button
            key={mode.value}
            type="button"
            className={mode.value === rounding ? 'segment segment-active' : 'segment'}
            aria-pressed={mode.value === rounding}
            title={mode.hint}
            onClick={() => onRoundingChange(mode.value)}
          >
            {mode.value}
          </button>
        ))}
      </div>

      <div className="search-field">
        <span className="search-icon" aria-hidden="true">
          ⌕
        </span>
        <input
          type="search"
          value={symbol}
          onChange={(event) => setSymbol(event.target.value)}
          placeholder="Search by symbol, e.g. AAPL..."
          aria-label="Stock symbol"
          autoComplete="off"
          spellCheck={false}
          disabled={busy}
        />
      </div>

      <button type="submit" className="button-primary" disabled={busy || !symbol.trim()}>
        {busy ? 'Searching…' : 'Search'}
      </button>

      <button type="button" className="button-secondary" onClick={onExport} disabled={!onExport}>
        <span aria-hidden="true">⤓</span> Export
      </button>
    </form>
  );
}
