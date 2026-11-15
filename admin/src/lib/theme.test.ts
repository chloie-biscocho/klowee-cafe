/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { signedIn } from '../test/fixtures'
import { renderApp } from '../test/renderApp'
import { THEME_KEY } from './theme'

// Vitest runs from admin/.
const INDEX = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')

/** Runs the inline script from index.html's head, as the browser would before first paint. */
function runHeadScript() {
  const script = INDEX.match(/<script>([\s\S]*?)<\/script>/)
  expect(script, 'index.html has an inline theme script').not.toBeNull()
  new Function(script![1]!)()
}

function filesUnder(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (name === 'node_modules') return []
    return statSync(path).isDirectory() ? filesUnder(path) : [path]
  })
}

afterEach(() => {
  delete document.documentElement.dataset.theme
  vi.unstubAllGlobals()
})

describe('the admin theme (handover 18)', () => {
  it('is light by default, even when the device prefers dark', () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query.includes('dark'), media: query, addEventListener() {}, removeEventListener() {} }))
    runHeadScript()
    expect(document.documentElement.dataset.theme).toBeUndefined()
  })

  it('applies a stored dark choice before first paint', () => {
    localStorage.setItem(THEME_KEY, 'dark')
    runHeadScript()
    expect(document.documentElement.dataset.theme).toBe('dark')
  })

  it('the head script and the app use the same key', () => {
    expect(INDEX).toContain(`'${THEME_KEY}'`)
  })

  it('the sidebar switch changes the theme and remembers it', async () => {
    const { user } = renderApp('/', signedIn)
    await user.click(await screen.findByRole('button', { name: 'Dark mode' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(localStorage.getItem(THEME_KEY)).toBe('dark')
    await user.click(screen.getByRole('button', { name: 'Light mode' }))
    expect(document.documentElement.dataset.theme).toBe('light')
    expect(localStorage.getItem(THEME_KEY)).toBe('light')
  })

  it('no style follows the device setting: no prefers-color-scheme anywhere', () => {
    const sources = [resolve(process.cwd(), 'index.html'), ...filesUnder(resolve(process.cwd(), 'src')).filter((f) => /\.(css|tsx?|html)$/.test(f) && !f.endsWith('theme.test.ts'))]
    const offenders = sources.filter((f) => readFileSync(f, 'utf8').includes('prefers-color-scheme'))
    expect(offenders).toEqual([])
    // Tailwind's `dark:` would otherwise compile to that media query.
    expect(readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8')).toMatch(/@custom-variant dark \(&:where\(\[data-theme='dark'\]/)
  })
})
