import type { ReactNode } from 'react';

interface NoticeProps {
  /** `warning` for a degraded but usable result, `error` for a request that produced nothing. */
  readonly tone: 'warning' | 'error';
  readonly children: ReactNode;
}

/**
 * A banner above the results.
 *
 * Both tones are announced to assistive technology, but only errors interrupt: a warning describes
 * a result the user can still read, while an error means there is nothing to read.
 */
export function Notice({ tone, children }: NoticeProps) {
  return (
    <div className={`notice notice-${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <span className="notice-icon" aria-hidden="true">
        {tone === 'error' ? '⊘' : '◷'}
      </span>
      <span>{children}</span>
    </div>
  );
}
