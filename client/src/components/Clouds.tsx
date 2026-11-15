import type { CSSProperties } from 'react'

/**
 * One cloud placed by data rather than by a `.cloud-N` class: its width, where
 * it sits, how strong it is and how it drifts. Shapes scale from the width
 * (index.css, `.cloud-free`).
 */
export interface CloudSpec {
  /** Distance from the top of the layer, as a percentage of its height. */
  top: number
  /** Distance from the left (or, with `right`, the right) edge, as a percentage of the layer's width. Negative hangs off the edge. */
  x: number
  right?: boolean
  /** Width in px on a computer; a phone caps it at 70% of the screen. */
  width: number
  opacity: number
  /** Sideways drift in px (negative drifts left) and the seconds one way takes. */
  drift: number
  seconds: number
  /** Puffs on the right instead of the left, so neighbours don't look stamped. */
  flip?: boolean
  /** Hidden on phones (760px and below), which get fewer clouds. */
  wide?: boolean
}

/**
 * Drifting clouds. With no `clouds`, the hero's six (shapes, positions and
 * drift in index.css, `.cloud-1` to `.cloud-6`), as the hero and the tab page
 * have always drawn them. With `clouds`, that fixed list instead.
 */
export function Clouds({ clouds }: { clouds?: readonly CloudSpec[] }) {
  if (!clouds) {
    return (
      <>
        {[1, 2, 3, 4, 5, 6].map((n) => (
          <div key={n} className={`cloud cloud-${n}`} aria-hidden="true" />
        ))}
      </>
    )
  }

  return (
    <>
      {clouds.map((cloud, index) => {
        const style = {
          '--w': `${cloud.width}px`,
          '--d': `${cloud.drift}px`,
          '--t': `${cloud.seconds}s`,
          top: `${cloud.top}%`,
          [cloud.right ? 'right' : 'left']: `${cloud.x}%`,
          opacity: cloud.opacity,
        } as CSSProperties
        const classes = ['cloud', 'cloud-free', cloud.flip && 'cloud-flip', cloud.wide && 'cloud-wide'].filter(Boolean).join(' ')
        return <div key={index} className={classes} style={style} aria-hidden="true" />
      })}
    </>
  )
}
