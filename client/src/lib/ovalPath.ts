/**
 * A hand-drawn marker oval around a label of `width` × `height`, as an SVG path
 * (ported from the approved mockup). One loop and a bit: it starts slightly
 * inside, wobbles a little, and finishes slightly outside, so the tail overlaps
 * the start like a real pen stroke. `seed` varies the wobble per label.
 *
 * Every point stays inside the box. The mockup used a fixed 4px margin, which
 * wide labels outgrow (at 300px the stroke poked 3px past the edge); here each
 * radius is the half-size divided by its largest growth (1.03 × the wobble:
 * 1.8% across, 5% down), less a 1px margin for the stroke's own smoothing.
 */
export function ovalPath(width: number, height: number, seed: number): string {
  const cx = width / 2
  const cy = height / 2
  const rx = width / 2 / (1.03 * 1.018) - 1
  const ry = height / 2 / (1.03 * 1.05) - 1
  const steps = 90
  const turns = 1.09
  const start = -2.25
  let d = ''
  for (let i = 0; i <= steps; i++) {
    const p = i / steps
    const t = start + p * turns * Math.PI * 2
    const grow = 0.955 + 0.075 * p
    const x = cx + rx * grow * (1 + 0.018 * Math.sin(3 * t + seed)) * Math.cos(t)
    const y = cy + ry * grow * (1 + 0.05 * Math.cos(2 * t + seed * 1.7)) * Math.sin(t)
    d += `${i ? 'L' : 'M'}${x.toFixed(1)} ${y.toFixed(1)}`
  }
  return d
}
