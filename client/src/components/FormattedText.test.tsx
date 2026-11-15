/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { PublicHome } from '../api/types'
import { Root } from '../Root'
import { toPlainText } from '../lib/formatted'
import { EventPanel } from './EventPanel'
import { Packages } from '../sections/Packages'
import { Story } from '../sections/Story'
import { event, fullHome } from '../test/fixtures'
import { server } from '../test/server'
import { gcash, tab, TAB_URL, TOKEN } from '../test/tabFixtures'

/** One text using every rule the owners asked for: a justified paragraph, bold, a link and a list. */
const RICH = '::justify\nWe brew **small batches** for [your event](https://klowee.example/book).\n\n- Coffee\n- Matcha'

function expectRich(scope: HTMLElement) {
  // Only the formatted text: a package card has its own list of inclusions too.
  const container = scope.querySelector<HTMLElement>('.fmt')!
  const bold = within(container).getByText('small batches')
  expect(bold.tagName).toBe('STRONG')
  const link = within(container).getByRole('link', { name: 'your event' })
  expect(link).toHaveAttribute('href', 'https://klowee.example/book')
  expect(link).toHaveAttribute('target', '_blank')
  expect(link).toHaveAttribute('rel', 'noopener noreferrer')
  const list = within(container).getByRole('list')
  expect(within(list).getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Coffee', 'Matcha'])
  expect(bold.closest('p')).toHaveClass('fmt-justify')
}

describe('formatted texts on the site (handover 21)', () => {
  it('the story body renders bold, a link, a list and a justified paragraph', () => {
    const { container } = render(<Story home={{ ...fullHome, settings: { ...fullHome.settings, story_body: RICH } }} />)
    expectRich(container)
  })

  it('an event description renders them too', () => {
    const { container } = render(<EventPanel event={event({ id: 'e-rich', name: 'Rich', description: RICH })} id="panel" onClose={() => {}} />)
    expectRich(container)
  })

  it('a package description shows on its card, formatted; no description, no extra space', () => {
    const home: PublicHome = { ...fullHome, packages: [{ ...fullHome.packages[0]!, description: RICH }, { ...fullHome.packages[0]!, id: 'p-2', name: 'Plain', description: '' }] }
    render(<Packages home={home} />)
    const [rich, plain] = screen.getAllByRole('heading', { level: 3 }).map((h) => h.closest('li')!)
    expectRich(rich!)
    expect(plain!.querySelector('.fmt')).toBeNull()
  })

  it('hostile text stays inert: no script element, no javascript: link', () => {
    const { container } = render(
      <Story home={{ ...fullHome, settings: { ...fullHome.settings, story_body: '<script>alert(1)</script> [x](javascript:alert(1))' } }} />,
    )
    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('a')).toBeNull()
    expect(screen.getByText('<script>alert(1)</script> [x](javascript:alert(1))')).toBeInTheDocument()
  })

  it('plain legacy text renders exactly as before: the same paragraphs, nothing else', () => {
    const { container } = render(<Story home={{ ...fullHome, settings: { ...fullHome.settings, story_body: 'First paragraph.\n\nSecond paragraph.' } }} />)
    const paragraphs = container.querySelectorAll('.fmt > p')
    expect([...paragraphs].map((p) => [p.textContent, p.className])).toEqual([
      ['First paragraph.', 'mt-3 first-of-type:mt-0'],
      ['Second paragraph.', 'mt-3 first-of-type:mt-0'],
    ])
    expect(container.querySelector('strong, em, a, ul, br')).toBeNull()
  })

  it("the tab page's note renders a link", async () => {
    server.use(http.get(TAB_URL, () => HttpResponse.json({ ...tab, pay: { note: 'Questions? [Message us](https://m.me/klowee)', methods: [gcash] } })))
    const user = userEvent.setup()
    render(<Root path={`/tab/${TOKEN}`} />)
    const receipt = (await screen.findByRole('heading', { name: 'your tab' })).closest('section')!
    await user.click(within(receipt).getByRole('button', { name: 'Pay with GCash' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByRole('link', { name: 'Message us' })).toHaveAttribute('href', 'https://m.me/klowee')
  })

  it("the page's meta description is plain text (it is fixed in index.html, never fed from these fields)", () => {
    const html = readFileSync(resolve(process.cwd(), 'index.html'), 'utf8')
    const description = /property="og:description"\s+content="([^"]*)"/.exec(html)![1]!
    expect(toPlainText(description)).toBe(description)
  })
})
