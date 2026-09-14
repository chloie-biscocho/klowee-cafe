/** Marks a published row as read-only; the title is the tooltip. */
export function LockIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      className="size-3.5 shrink-0"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      role="img"
      aria-label="Locked"
    >
      <title>published versions can&apos;t be edited; copy it to make changes</title>
      <rect x="3" y="7" width="10" height="7" rx="1.5" />
      <path d="M5.5 7V5a2.5 2.5 0 0 1 5 0v2" strokeLinecap="round" />
    </svg>
  )
}
