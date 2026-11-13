/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { THEME_COLOR, THEME_KEY } from '../lib/theme'
import { ThemeSwitch } from './ThemeSwitch'

// Vitest runs from client/.
const INDEX = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
const CSS = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')

const root = document.documentElement
const themeColor = () => document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')!.content

/** The device says it prefers dark, which the site must now ignore. */
function preferDark() {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: query.includes('dark'),
    media: query,
    addEventListener() {},
    removeEventListener() {},
  }))
}

/** Runs the inline script from index.html's head, as the browser would before first paint. */
function runHeadScript() {
  const script = INDEX.match(/<script>([\s\S]*?)<\/script>/)
  expect(script, 'index.html has an inline theme script').not.toBeNull()
  new Function(script![1]!)()
}

beforeEach(() => {
  localStorage.clear()
  delete root.dataset.theme
  document.head.innerHTML = `<meta name="theme-color" content="${THEME_COLOR.light}" />`
  preferDark()
})

afterEach(() => vi.unstubAllGlobals())

describe('theme', () => {
  it('is light on a first visit, even when the device prefers dark', () => {
    runHeadScript()
    render(<ThemeSwitch />)

    expect(root.dataset.theme).not.toBe('dark')
    expect(themeColor()).toBe(THEME_COLOR.light)
    expect(screen.getByRole('button', { name: 'Dark mode' })).toHaveTextContent('☾ Dark mode')
  })

  it('switches to dark on click, remembers it, and back again', async () => {
    const user = userEvent.setup()
    render(<ThemeSwitch />)

    await user.click(screen.getByRole('button', { name: 'Dark mode' }))
    expect(root.dataset.theme).toBe('dark')
    expect(localStorage.getItem(THEME_KEY)).toBe('dark')
    expect(themeColor()).toBe(THEME_COLOR.dark)
    expect(screen.getByRole('button', { name: 'Light mode' })).toHaveTextContent('☀ Light mode')

    await user.click(screen.getByRole('button', { name: 'Light mode' }))
    expect(root.dataset.theme).toBe('light')
    expect(localStorage.getItem(THEME_KEY)).toBe('light')
    expect(themeColor()).toBe(THEME_COLOR.light)
    expect(screen.getByRole('button', { name: 'Dark mode' })).toBeInTheDocument()
  })

  it('applies a stored dark choice before first paint, and labels the switch to match', () => {
    localStorage.setItem(THEME_KEY, 'dark')
    runHeadScript()

    expect(root.dataset.theme).toBe('dark')
    expect(themeColor()).toBe(THEME_COLOR.dark)
    render(<ThemeSwitch />)
    expect(screen.getByRole('button', { name: 'Light mode' })).toBeInTheDocument()
  })

  it.each([['light'], ['nonsense']])('gives light for a stored %s', (stored) => {
    localStorage.setItem(THEME_KEY, stored)
    runHeadScript()
    render(<ThemeSwitch />)

    expect(root.dataset.theme).not.toBe('dark')
    expect(themeColor()).toBe(THEME_COLOR.light)
    expect(screen.getByRole('button', { name: 'Dark mode' })).toBeInTheDocument()
  })

  it('still switches when storage throws', async () => {
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    const user = userEvent.setup()
    render(<ThemeSwitch />)

    await user.click(screen.getByRole('button', { name: 'Dark mode' }))
    expect(root.dataset.theme).toBe('dark')
    vi.restoreAllMocks()
  })

  it('keeps dark tokens behind the switch, not the device setting', () => {
    expect(CSS).not.toMatch(/prefers-color-scheme/)
    expect(CSS).toMatch(/:root\[data-theme='dark'\]\s*\{[^}]*color-scheme: dark;/)
    expect(CSS).toMatch(/:root\s*\{[^}]*color-scheme: light;/)
    expect(INDEX).not.toMatch(/prefers-color-scheme/)
  })
})
