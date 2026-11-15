import type { CloudSpec } from '../components/Clouds'

/**
 * Where the clouds below the hero sit (handover 19): a fixed list, so the sky
 * is the same on every visit and in tests. Scattered rather than tiled:
 * uneven gaps, most clouds hanging off the edges, a few large and very soft
 * ones, and only small, faint ones near the middle. Big clouds drift slower
 * than small ones. `wide` clouds are left out on phones.
 *
 * Every text colour that sits directly on the sky passes AA over a cloud at
 * any opacity, in both themes (worst case: dark `soft` on a full cloud,
 * 6.26:1), so no band is kept clear for text. The first cloud starts below
 * the cup shelf, whose sky-coloured fill cover would show over a cloud.
 */
export const PAGE_CLOUDS: readonly CloudSpec[] = [
  { top: 6, x: -6, width: 170, opacity: 0.9, drift: 120, seconds: 32 },
  { top: 9, x: -4, right: true, width: 120, opacity: 0.85, drift: -90, seconds: 27, flip: true, wide: true },
  { top: 15, x: 6, right: true, width: 300, opacity: 0.45, drift: -70, seconds: 48 },
  { top: 19, x: 3, width: 110, opacity: 0.8, drift: 80, seconds: 25, wide: true },
  { top: 24, x: -10, width: 340, opacity: 0.4, drift: 60, seconds: 52, flip: true },
  { top: 28, x: -6, right: true, width: 150, opacity: 0.9, drift: -110, seconds: 34 },
  { top: 33, x: 38, width: 90, opacity: 0.55, drift: 70, seconds: 24, wide: true },
  { top: 37, x: 2, right: true, width: 200, opacity: 0.7, drift: -100, seconds: 38, flip: true, wide: true },
  { top: 42, x: -4, width: 140, opacity: 0.85, drift: 100, seconds: 30 },
  { top: 48, x: -12, right: true, width: 320, opacity: 0.4, drift: -60, seconds: 50 },
  { top: 52, x: 8, width: 100, opacity: 0.75, drift: 70, seconds: 26, flip: true, wide: true },
  { top: 57, x: -8, width: 260, opacity: 0.5, drift: 80, seconds: 44, wide: true },
  { top: 62, x: 4, right: true, width: 130, opacity: 0.85, drift: -90, seconds: 29, flip: true },
  { top: 67, x: 46, width: 110, opacity: 0.5, drift: -60, seconds: 27, wide: true },
  { top: 71, x: -5, width: 180, opacity: 0.8, drift: 110, seconds: 36 },
  { top: 76, x: -8, right: true, width: 280, opacity: 0.45, drift: -70, seconds: 46, flip: true, wide: true },
  { top: 82, x: 5, width: 120, opacity: 0.8, drift: 90, seconds: 28, wide: true },
  { top: 86, x: 3, right: true, width: 160, opacity: 0.85, drift: -100, seconds: 33, flip: true },
  { top: 93, x: -10, width: 300, opacity: 0.4, drift: 60, seconds: 50, wide: true },
]
