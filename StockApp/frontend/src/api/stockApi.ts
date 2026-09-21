import type { ApiError, ApiErrorCode, DailySummary, DailySummaryResult, RoundingMode } from './types';

/**
 * Where the API lives. Set `VITE_API_BASE_URL` to point a build at a deployed instance; the default
 * matches the backend's development profile.
 */
const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5264';

/** Failures the caller can reasonably retry, as opposed to ones that need a different request. */
const retryableCodes: ReadonlySet<ApiErrorCode> = new Set<ApiErrorCode>([
  'UPSTREAM_UNAVAILABLE',
  'UPSTREAM_RATE_LIMITED',
  'NETWORK',
]);

/**
 * Thrown for every unsuccessful request, so callers catch one type and read one shape.
 */
export class StockApiError extends Error implements ApiError {
  readonly errorCode: ApiErrorCode;
  readonly title: string;
  readonly detail?: string;
  readonly retryable: boolean;

  constructor(error: ApiError) {
    super(error.detail ?? error.title);
    this.name = 'StockApiError';
    this.errorCode = error.errorCode;
    this.title = error.title;
    this.detail = error.detail;
    this.retryable = error.retryable;
  }
}

/**
 * Fetches one month of daily summaries for a symbol.
 *
 * @param symbol   The ticker to query. Validation is the API's job; whatever is typed is sent.
 * @param rounding How prices are reduced. Omitted means the API's configured default.
 * @param signal   Aborts the request when the caller moves on, e.g. a second search.
 */
export async function fetchDailySummaries(
  symbol: string,
  rounding?: RoundingMode,
  signal?: AbortSignal,
): Promise<DailySummaryResult> {
  const url = new URL(`/api/v1/stocks/${encodeURIComponent(symbol)}/daily`, baseUrl);
  if (rounding) {
    url.searchParams.set('rounding', rounding);
  }

  let response: Response;
  try {
    response = await fetch(url, { signal, headers: { Accept: 'application/json' } });
  } catch (cause) {
    // An aborted request is the caller's own doing, so it is passed through rather than
    // reported as a failure the user should see.
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }

    throw new StockApiError({
      errorCode: 'NETWORK',
      title: 'Cannot reach the server',
      detail: 'The API did not respond. Check that it is running and try again.',
      retryable: true,
    });
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  const days = (await response.json()) as DailySummary[];

  return {
    symbol: symbol.trim().toUpperCase(),
    days,
    groupingTimeZone: response.headers.get('X-Grouping-Timezone') ?? undefined,
    groupedByUtcFallback: response.headers.get('X-Grouping-Timezone-Fallback') === 'true',
    exchangeName: response.headers.get('X-Exchange-Name') ?? undefined,
  };
}

/**
 * Turns a failed response into an error the UI can act on.
 *
 * The API answers failures with an RFC 9457 problem document carrying a stable `errorCode`. Reading
 * that code, rather than the status alone, is what lets the UI tell a changed upstream API apart
 * from a temporary outage — both are 502s, but only one is worth retrying.
 */
async function toApiError(response: Response): Promise<StockApiError> {
  let problem: { errorCode?: ApiErrorCode; title?: string; detail?: string } = {};

  try {
    problem = await response.json();
  } catch {
    // A failure without a readable body still has a status, which is enough to report.
  }

  const errorCode = problem.errorCode ?? 'UNKNOWN';

  return new StockApiError({
    errorCode,
    title: problem.title ?? `Request failed (${response.status})`,
    detail: problem.detail,
    retryable: retryableCodes.has(errorCode),
  });
}
