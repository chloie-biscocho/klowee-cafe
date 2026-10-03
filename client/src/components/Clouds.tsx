/** Three soft white clouds for a sky band. Static and decorative. */
export function Clouds() {
  return (
    <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden">
      <Cloud className="top-8 -left-10 w-48 opacity-60 md:left-[6%]" />
      <Cloud className="top-24 -right-12 w-56 opacity-50 md:right-[8%]" />
      <Cloud className="bottom-10 left-[38%] hidden w-40 opacity-40 sm:block" />
    </div>
  )
}

function Cloud({ className }: { className: string }) {
  return (
    <svg viewBox="0 0 200 80" className={`absolute fill-white ${className}`}>
      <ellipse cx="60" cy="50" rx="50" ry="28" />
      <ellipse cx="105" cy="38" rx="45" ry="34" />
      <ellipse cx="150" cy="52" rx="42" ry="24" />
      <rect x="20" y="50" width="170" height="28" rx="14" />
    </svg>
  )
}
