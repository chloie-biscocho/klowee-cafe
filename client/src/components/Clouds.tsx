/** The hero's six clouds. Shapes, positions and drift live in index.css (`.cloud-1` to `.cloud-6`). */
export function Clouds() {
  return (
    <>
      {[1, 2, 3, 4, 5, 6].map((n) => (
        <div key={n} className={`cloud cloud-${n}`} aria-hidden="true" />
      ))}
    </>
  )
}
