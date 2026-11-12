import { useEffect, type RefObject } from 'react'
import { ovalPath } from '../../lib/ovalPath'

/**
 * Sizes each category's pink oval to its label (`[data-oval]` inside the
 * sheet): measure the label, set the SVG's viewBox, generate the path. Drawn
 * once now, again when the web fonts arrive (the label changes width), and on
 * resize. The sheet gets `inked` the first time it enters the viewport, which
 * starts the CSS draw-in; reduced motion skips the animation in CSS.
 */
export function useOvals(sheet: RefObject<HTMLElement | null>, key: unknown) {
  useEffect(() => {
    const el = sheet.current
    if (!el) return

    const draw = () => {
      el.querySelectorAll<HTMLElement>('[data-oval]').forEach((label, index) => {
        const width = label.offsetWidth
        const height = label.offsetHeight
        const svg = label.querySelector('svg')
        const path = svg?.querySelector('path')
        if (!svg || !path || width === 0 || height === 0) return
        svg.setAttribute('viewBox', `0 0 ${width} ${height}`)
        path.setAttribute('d', ovalPath(width, height, index * 1.3 + 0.4))
        svg.style.transform = `rotate(${index % 2 ? 1.2 : -1.6}deg)`
      })
    }

    let cancelled = false
    draw()
    document.fonts?.ready.then(() => !cancelled && draw())
    window.addEventListener('resize', draw)

    let observer: IntersectionObserver | undefined
    if (typeof IntersectionObserver === 'undefined') {
      el.classList.add('inked')
    } else {
      observer = new IntersectionObserver(
        (entries) => {
          if (entries[0]?.isIntersecting) {
            el.classList.add('inked')
            observer?.disconnect()
          }
        },
        { threshold: 0.12 },
      )
      observer.observe(el)
    }

    return () => {
      cancelled = true
      window.removeEventListener('resize', draw)
      observer?.disconnect()
    }
  }, [sheet, key])
}
