/** One trading day, exactly as the API publishes it. */
export interface DailySummary {
  /** Exchange-local trading date, as `yyyy-MM-dd`. */
  readonly day: string;
  /** Mean of the day's interval lows, already reduced to the published precision. */
  readonly lowAverage: number;
  /** Mean of the day's interval highs, already reduced to the published precision. */
  readonly highAverage: number;
  /** Total shares traded during the day. */
  readonly volume: number;
}

/**
 * How prices are reduced to four decimal places.
 *
 * These are the .NET `MidpointRounding` names the API accepts. `AwayFromZero` rounds halves up and
 * is the default; `ToZero` truncates, cutting the value off rather than rounding it.
 */
export type RoundingMode = 'AwayFromZero' | 'ToZero';

/**
 * The stable error identifiers the API publishes alongside every failure.
 *
 * Clients branch on these rather than on the human-readable title, which may be reworded.
 */
export type ApiErrorCode =
  | 'INVALID_SYMBOL'
  | 'INVALID_ROUNDING'
  | 'SYMBOL_NOT_FOUND'
  | 'UPSTREAM_RATE_LIMITED'
  | 'UPSTREAM_UNAVAILABLE'
  | 'UPSTREAM_CONTRACT'
  | 'NETWORK'
  | 'UNKNOWN';

/** A failed request, normalised so every caller handles one shape. */
export interface ApiError {
  readonly errorCode: ApiErrorCode;
  /** Short heading, safe to show to a user. */
  readonly title: string;
  /** The explanation, where the API supplied one. */
  readonly detail?: string;
  /** Whether trying the same request again could plausibly succeed. */
  readonly retryable: boolean;
}

/** A successful query: the days, plus how the API grouped them. */
export interface DailySummaryResult {
  readonly symbol: string;
  readonly days: readonly DailySummary[];
  /** The IANA zone the days were grouped on, as reported by the data source. */
  readonly groupingTimeZone?: string;
  /** True when the exchange's zone could not be resolved and UTC was used instead. */
  readonly groupedByUtcFallback: boolean;
  /** The exchange's display name, such as `NasdaqGS`, when the data source reports one. */
  readonly exchangeName?: string;
}

/** The columns the results table can be ordered by. */
export type SortColumn = 'day' | 'lowAverage' | 'highAverage' | 'volume';

/** Which column orders the table, and in which direction. */
export interface SortState {
  readonly column: SortColumn;
  readonly direction: 'asc' | 'desc';
}
