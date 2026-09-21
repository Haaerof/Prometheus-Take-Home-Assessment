import { useCallback, useMemo, useRef, useState } from 'react';
import { fetchDailySummaries, StockApiError } from './api/stockApi';
import type { ApiError, DailySummaryResult, RoundingMode, SortColumn } from './api/types';
import { DailySummaryTable } from './components/DailySummaryTable';
import { Notice } from './components/Notice';
import { SymbolSearchForm } from './components/SymbolSearchForm';
import { downloadJson } from './components/jsonExport';
import { defaultSort, nextSort, sortDays } from './components/sorting';
import './App.css';

/**
 * The application shell: it owns the query, the result and the failure, and decides which of the
 * three the page is currently showing.
 */
export default function App() {
  const [result, setResult] = useState<DailySummaryResult | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);
  const [rounding, setRounding] = useState<RoundingMode>('AwayFromZero');
  const [lastSymbol, setLastSymbol] = useState<string | null>(null);
  const [sort, setSort] = useState(defaultSort);

  // Lets a newer search cancel one still in flight, so a slow first response cannot overwrite a
  // faster second one.
  const inFlight = useRef<AbortController | null>(null);

  const search = useCallback(async (symbol: string, mode: RoundingMode) => {
    inFlight.current?.abort();
    const controller = new AbortController();
    inFlight.current = controller;

    setBusy(true);
    setError(null);
    setLastSymbol(symbol);

    try {
      setResult(await fetchDailySummaries(symbol, mode, controller.signal));
    } catch (caught) {
      if (caught instanceof DOMException && caught.name === 'AbortError') {
        return; // Superseded by a newer search; the newer one owns the outcome.
      }

      setResult(null);
      setError(
        caught instanceof StockApiError
          ? caught
          : {
              errorCode: 'UNKNOWN',
              title: 'Something went wrong',
              detail: 'The results could not be loaded.',
              retryable: true,
            },
      );
    } finally {
      if (inFlight.current === controller) {
        setBusy(false);
      }
    }
  }, []);

  /** Switching strategy re-queries, because rounding happens server-side and cannot be undone here. */
  function handleRoundingChange(mode: RoundingMode) {
    setRounding(mode);
    if (lastSymbol) {
      void search(lastSymbol, mode);
    }
  }

  // Sorted once, then shown and exported, so a download always matches what is on screen.
  const orderedDays = useMemo(() => sortDays(result?.days ?? [], sort), [result, sort]);
  const hasRows = orderedDays.length > 0;

  return (
    <main className="page">
      <header className="page-header">
        {/*
          Names what is on screen once there is something on screen. Before a search it renders
          empty rather than showing placeholder notation, but still occupies its line so the
          heading does not jump when the first result arrives.
        */}
        <p className="eyebrow">
          {result && (
            <>
              <span>{result.symbol}</span>
              {result.exchangeName && <span> · {result.exchangeName}</span>}
            </>
          )}
        </p>
        <h1>Query Last Month&apos;s Intraday Data</h1>
      </header>

      <SymbolSearchForm
        busy={busy}
        rounding={rounding}
        onRoundingChange={handleRoundingChange}
        onSearch={(symbol) => void search(symbol, rounding)}
        onExport={result && hasRows ? () => downloadJson(result.symbol, orderedDays) : undefined}
      />

      {error && (
        <Notice tone="error">
          <strong>{error.title}</strong>
          {error.detail && <span className="notice-detail"> {error.detail}</span>}
        </Notice>
      )}

      {result && !hasRows && !error && (
        <Notice tone="error">
          <strong>No data for that symbol</strong>
        </Notice>
      )}

      {result?.groupedByUtcFallback && (
        <Notice tone="warning">
          Exchange timezone unavailable — grouped by UTC. Day boundaries may be off for this exchange.
        </Notice>
      )}

      {/* Always rendered: the column headings describe the answer before one has been asked for. */}
      <DailySummaryTable
        days={orderedDays}
        sort={sort}
        onSortChange={(column: SortColumn) => setSort((current) => nextSort(current, column))}
      />
    </main>
  );
}
