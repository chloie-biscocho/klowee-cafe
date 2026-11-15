import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PAGE_CLOUDS } from '../lib/pageClouds'
import { Clouds } from './Clouds'
import { PageSky } from './PageSky'

describe('Clouds', () => {
  it("still draws the hero's six by default (the hero and the tab page rely on them)", () => {
    const { container } = render(<Clouds />)

    expect([...container.children].map((cloud) => cloud.className)).toEqual([1, 2, 3, 4, 5, 6].map((n) => `cloud cloud-${n}`))
    for (const cloud of container.children) expect(cloud).toHaveAttribute('aria-hidden', 'true')
  })
})

describe('PageSky', () => {
  it('renders the fixed set of clouds, decorative and out of the way', () => {
    render(<PageSky />)

    const sky = screen.getByTestId('page-sky')
    expect(sky).toHaveAttribute('aria-hidden', 'true')
    expect(sky).toHaveClass('pointer-events-none', 'absolute', '-z-10', 'overflow-x-clip')
    const clouds = [...sky.querySelectorAll('.cloud')] as HTMLElement[]
    expect(clouds).toHaveLength(PAGE_CLOUDS.length)
    // Placed from the list, so every render (and every visitor) gets the same sky.
    expect(clouds.map((cloud) => [cloud.style.top, cloud.style.left || `r${cloud.style.right}`, cloud.style.getPropertyValue('--w')])).toEqual(
      PAGE_CLOUDS.map((c) => [`${c.top}%`, c.right ? `r${c.x}%` : `${c.x}%`, `${c.width}px`]),
    )
  })

  it('gives phones a smaller set than computers', () => {
    render(<PageSky />)

    const clouds = [...screen.getByTestId('page-sky').querySelectorAll('.cloud')]
    // `.cloud-wide` is hidden at 760px and below (index.css).
    const phone = clouds.filter((cloud) => !cloud.classList.contains('cloud-wide'))
    expect(phone.length).toBeGreaterThanOrEqual(6)
    expect(phone.length).toBeLessThan(clouds.length)
    expect(clouds.length).toBe(19)
    expect(phone.length).toBe(9)
  })

  it('scatters rather than tiles: varied sizes, most clouds near the edges, a few large and soft', () => {
    const widths = new Set(PAGE_CLOUDS.map((c) => c.width))
    expect(widths.size).toBeGreaterThan(10)
    const nearEdge = PAGE_CLOUDS.filter((c) => c.x <= 10)
    expect(nearEdge.length).toBeGreaterThan(PAGE_CLOUDS.length * 0.8)
    expect(PAGE_CLOUDS.filter((c) => c.width >= 260 && c.opacity <= 0.5).length).toBeGreaterThanOrEqual(3)
    // Big clouds drift slower than small ones.
    const slowest = (min: number) => Math.min(...PAGE_CLOUDS.filter((c) => c.width >= min).map((c) => c.seconds))
    expect(slowest(260)).toBeGreaterThan(Math.max(...PAGE_CLOUDS.filter((c) => c.width < 130).map((c) => c.seconds)))
  })
})
