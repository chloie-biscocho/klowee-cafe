/**
 * The mark inside a stamped slot: a small coffee cup. One component, so the
 * cup can become the logo later in one place (handover 16b). Decorative: the
 * slot itself carries the text label.
 */
export function StampMark({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" focusable="false" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M4 9h12v5a5 5 0 0 1-5 5H9a5 5 0 0 1-5-5V9Z" />
      <path d="M16 11h1.5a2.5 2.5 0 0 1 0 5H16" />
      <path d="M8 3.5c0 1 1 1.2 1 2.3M12 3.5c0 1 1 1.2 1 2.3" />
    </svg>
  )
}
