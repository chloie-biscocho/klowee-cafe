/// <reference types="node" />
import { readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { App } from './App'

/**
 * index.html carries the page's loading render (skip link, header, the hero's
 * shell with its logo, the loading blocks) inside #root, so the first screen
 * paints from HTML before any JavaScript runs. React then replaces it with the
 * same markup, so nothing moves. This keeps the copy honest: it must equal
 * App's loading render, minus the footer (whose year would go stale).
 *
 * After changing the header, the hero or the loading state, regenerate it:
 *   UPDATE_SHELL=1 npx vitest run src/shell.test.tsx
 */
// Vitest runs from client/.
const INDEX = resolve(process.cwd(), 'index.html')
const START = '<div id="root">'
const END = '</div>\n    <script type="module"'

function loadingShell(): string {
  const markup = renderToStaticMarkup(<App />)
  return markup.replace(/<footer[\s\S]*<\/footer>/, '')
}

function currentShell(html: string): string {
  return html.slice(html.indexOf(START) + START.length, html.indexOf(END))
}

describe('the static shell in index.html', () => {
  it("is App's loading render", () => {
    const html = readFileSync(INDEX, 'utf8')
    const expected = loadingShell()
    if (process.env.UPDATE_SHELL) {
      const from = html.indexOf(START) + START.length
      writeFileSync(INDEX, html.slice(0, from) + expected + html.slice(html.indexOf(END)))
      return
    }
    expect(currentShell(html)).toBe(expected)
  })
})
