import { describe, expect, it } from 'vitest'
import { ovalPath } from './ovalPath'

const points = (d: string) =>
  [...d.matchAll(/[ML](-?[\d.]+) (-?[\d.]+)/g)].map((m) => ({ x: Number(m[1]), y: Number(m[2]) }))

describe('ovalPath', () => {
  it.each([
    [60, 30],
    [120, 44],
    [200, 46],
    [300, 60],
    [480, 52],
  ])('stays inside a %i × %i box and closes on itself', (width, height) => {
    for (const seed of [0.4, 1.7, 3, 6.9]) {
      const pts = points(ovalPath(width, height, seed))
      expect(pts).toHaveLength(91)
      for (const { x, y } of pts) {
        expect(x).toBeGreaterThanOrEqual(0)
        expect(x).toBeLessThanOrEqual(width)
        expect(y).toBeGreaterThanOrEqual(0)
        expect(y).toBeLessThanOrEqual(height)
      }
      // One loop and a bit: the end comes back past the start, so the stroke overlaps like a pen.
      const first = pts[0]!
      const last = pts.at(-1)!
      expect(Math.hypot(last.x - first.x, last.y - first.y)).toBeLessThan(Math.max(width, height) * 0.6)
      // It goes all the way round: points on every side of the centre.
      expect(Math.min(...pts.map((p) => p.x))).toBeLessThan(width * 0.1)
      expect(Math.max(...pts.map((p) => p.x))).toBeGreaterThan(width * 0.9)
    }
  })
})
