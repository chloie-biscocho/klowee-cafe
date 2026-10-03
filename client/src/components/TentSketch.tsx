/**
 * The pop-up tent as a line doodle, for empty states. Hand-drawn feel, cobalt
 * strokes only, so it reads as an illustration rather than an icon.
 */
export function TentSketch({ className = '' }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 160 120"
      aria-hidden="true"
      className={`fill-none stroke-cobalt ${className}`}
      strokeWidth="3"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M20 46 L80 14 L140 46" />
      <path d="M20 46 q10 10 20 0 q10 10 20 0 q10 10 20 0 q10 10 20 0 q10 10 20 0 q10 10 20 0" />
      <path d="M28 52 L28 106 M132 52 L132 106" />
      <path d="M44 78 h72 v28 h-72 z" />
      <path d="M58 70 q4 -6 8 0 M92 70 q4 -6 8 0" />
      <path d="M10 108 h140" />
    </svg>
  )
}
