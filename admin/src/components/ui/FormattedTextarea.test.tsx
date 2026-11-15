import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { beforeEach, describe, expect, it } from 'vitest'
import { alignmentAt, applyEdit, setAlignment, toggleLink, toggleList, toggleWrap } from './formatEdits'
import { FormattedTextarea } from './FormattedTextarea'

/** A controlled field, as the tab page note uses it. */
function Harness({ initial = '' }: { initial?: string }) {
  const [value, setValue] = useState(initial)
  return (
    <>
      <FormattedTextarea label="Body" value={value} onChange={(event) => setValue(event.target.value)} />
      <output data-testid="stored">{value}</output>
    </>
  )
}

function setup(initial = '') {
  const user = userEvent.setup()
  render(<Harness initial={initial} />)
  const box = screen.getByLabelText('Body') as HTMLTextAreaElement
  const stored = () => screen.getByTestId('stored').textContent
  const select = (from: number, to = from) => {
    box.focus()
    box.setSelectionRange(from, to)
    fireEvent.select(box)
  }
  return { user, box, stored, select }
}

describe('the text edits', () => {
  const run = (value: string, edit: ReturnType<typeof toggleWrap>) => [applyEdit(value, edit), edit.selectStart, edit.selectEnd] as const

  it('bold and italic wrap, unwrap, or insert an example', () => {
    expect(run('big matcha', toggleWrap('big matcha', 4, 10, '**', 'bold text'))).toEqual(['big **matcha**', 6, 12])
    expect(run('big **matcha**', toggleWrap('big **matcha**', 6, 12, '**', 'bold text'))).toEqual(['big matcha', 4, 10])
    expect(run('big **matcha**', toggleWrap('big **matcha**', 4, 14, '**', 'bold text'))).toEqual(['big matcha', 4, 10])
    expect(run('ab', toggleWrap('ab', 1, 1, '**', 'bold text'))).toEqual(['a**bold text**b', 3, 12])
    expect(run('big matcha', toggleWrap('big matcha', 4, 10, '*', 'italic text'))).toEqual(['big *matcha*', 5, 11])
    // Spaces at the edges stay outside the markers.
    expect(run('big matcha ', toggleWrap('big matcha ', 3, 11, '**', 'bold text'))[0]).toBe('big **matcha** ')
    // Italic doesn't take half of a bold pair.
    expect(run('**matcha**', toggleWrap('**matcha**', 2, 8, '*', 'x'))[0]).toBe('***matcha***')
  })

  it('a link wraps the selection, inserts an example, or comes off', () => {
    expect(applyEdit('see menu', toggleLink('see menu', 4, 8, 'https://k.example'))).toBe('see [menu](https://k.example)')
    expect(applyEdit('', toggleLink('', 0, 0, 'https://k.example'))).toBe('[link text](https://k.example)')
    expect(applyEdit('[menu](https://k.example)', toggleLink('[menu](https://k.example)', 0, 25, null))).toBe('menu')
  })

  it('a list toggles on the touched lines', () => {
    expect(applyEdit('Coffee\nMatcha', toggleList('Coffee\nMatcha', 0, 13))).toBe('- Coffee\n- Matcha')
    expect(applyEdit('- Coffee\n- Matcha', toggleList('- Coffee\n- Matcha', 3, 3))).toBe('Coffee\n- Matcha')
    expect(applyEdit('- Coffee\n- Matcha', toggleList('- Coffee\n- Matcha', 0, 17))).toBe('Coffee\nMatcha')
    expect(applyEdit('', toggleList('', 0, 0))).toBe('- ')
  })

  it('alignment sets, changes and removes the paragraph marker, per paragraph', () => {
    const text = 'One.\n\nTwo.'
    expect(applyEdit(text, setAlignment(text, 1, 1, 'justify'))).toBe('::justify\nOne.\n\nTwo.')
    const justified = '::justify\nOne.\n\nTwo.'
    expect(alignmentAt(justified, 12)).toBe('justify')
    expect(alignmentAt(justified, 18)).toBe('left')
    expect(applyEdit(justified, setAlignment(justified, 12, 12, 'center'))).toBe('::center\nOne.\n\nTwo.')
    expect(applyEdit(justified, setAlignment(justified, 12, 12, 'justify'))).toBe('One.\n\nTwo.')
    expect(applyEdit(justified, setAlignment(justified, 12, 12, 'left'))).toBe('One.\n\nTwo.')
    // Several paragraphs selected: each gets it.
    expect(applyEdit(text, setAlignment(text, 0, 10, 'center'))).toBe('::center\nOne.\n\n::center\nTwo.')
  })
})

describe('FormattedTextarea', () => {
  beforeEach(() => localStorage.clear())

  it('Bold, Italic and List act on the selection, and insert an example at an empty cursor', async () => {
    const { user, stored, select } = setup('big matcha')

    select(4, 10)
    await user.click(screen.getByRole('button', { name: 'Bold (Ctrl+B)' }))
    expect(stored()).toBe('big **matcha**')
    select(6, 12)
    await user.click(screen.getByRole('button', { name: 'Bold (Ctrl+B)' }))
    expect(stored()).toBe('big matcha')

    select(4, 10)
    await user.click(screen.getByRole('button', { name: 'Italic (Ctrl+I)' }))
    expect(stored()).toBe('big *matcha*')

    select(12)
    await user.click(screen.getByRole('button', { name: 'Bold (Ctrl+B)' }))
    expect(stored()).toBe('big *matcha***bold text**')

    select(0, 3)
    await user.click(screen.getByRole('button', { name: 'Bulleted list' }))
    expect(stored()).toBe('- big *matcha***bold text**')
  })

  it('the shortcuts Ctrl+B, Ctrl+I and Ctrl+K do the same', async () => {
    const { user, stored, select } = setup('big matcha')
    select(4, 10)
    await user.keyboard('{Control>}b{/Control}')
    expect(stored()).toBe('big **matcha**')
    select(0, 3)
    await user.keyboard('{Control>}i{/Control}')
    expect(stored()).toBe('*big* **matcha**')
    select(0, 5)
    await user.keyboard('{Control>}k{/Control}')
    expect(screen.getByLabelText('Link address')).toHaveFocus()
  })

  it('Link asks for the address inline, adds it, and refuses anything but http, https and mailto', async () => {
    const { user, stored, select } = setup('see our menu')
    select(8, 12)
    await user.click(screen.getByRole('button', { name: 'Link (Ctrl+K)' }))

    const address = screen.getByLabelText('Link address')
    await user.type(address, 'javascript:alert(1){Enter}')
    expect(screen.getByRole('alert')).toHaveTextContent('Use a full address starting with https://, http:// or mailto:.')
    expect(stored()).toBe('see our menu')

    await user.clear(address)
    await user.type(address, 'https://klowee.example/#menu{Enter}')
    expect(stored()).toBe('see our [menu](https://klowee.example/#menu)')
    expect(screen.queryByLabelText('Link address')).not.toBeInTheDocument()
  })

  it('a link with nothing selected inserts example words to type over', async () => {
    const { user, stored, select } = setup('')
    select(0)
    await user.click(screen.getByRole('button', { name: 'Link (Ctrl+K)' }))
    await user.type(screen.getByLabelText('Link address'), 'mailto:hello@klowee.example')
    await user.click(screen.getByRole('button', { name: 'Add link' }))
    expect(stored()).toBe('[link text](mailto:hello@klowee.example)')
  })

  it("alignment acts on the cursor's paragraph, and the active one shows pressed", async () => {
    const { user, stored, select } = setup('One.\n\nTwo.')
    const left = () => screen.getByRole('button', { name: 'Align left' })
    const justify = () => screen.getByRole('button', { name: 'Justify' })
    const centre = () => screen.getByRole('button', { name: 'Centre' })

    select(1)
    expect(left()).toHaveAttribute('aria-pressed', 'true')
    await user.click(justify())
    expect(stored()).toBe('::justify\nOne.\n\nTwo.')
    expect(justify()).toHaveAttribute('aria-pressed', 'true')
    expect(left()).toHaveAttribute('aria-pressed', 'false')

    await user.click(centre())
    expect(stored()).toBe('::center\nOne.\n\nTwo.')
    expect(centre()).toHaveAttribute('aria-pressed', 'true')

    await user.click(centre())
    expect(stored()).toBe('One.\n\nTwo.')
    expect(left()).toHaveAttribute('aria-pressed', 'true')
  })

  it('the preview is drawn by the renderer, and closing it is remembered', async () => {
    const { user } = setup('::center\nBig **matcha**, see [menu](https://k.example).\n\n- Coffee\n- Matcha')
    const preview = screen.getByRole('region', { name: 'Body preview' })
    expect(within(preview).getByText('matcha').tagName).toBe('STRONG')
    expect(within(preview).getByRole('link', { name: 'menu' })).toHaveAttribute('href', 'https://k.example')
    expect(within(preview).getByRole('link', { name: 'menu' })).toHaveAttribute('rel', 'noopener noreferrer')
    expect(within(preview).getAllByRole('listitem').map((li) => li.textContent)).toEqual(['Coffee', 'Matcha'])
    expect(within(preview).getByText(/Big/).closest('p')).toHaveClass('fmt-center')

    await user.click(screen.getByRole('button', { name: 'Preview' }))
    expect(screen.queryByRole('region', { name: 'Body preview' })).not.toBeInTheDocument()
    expect(localStorage.getItem('klowee.admin.formatPreview')).toBe('closed')
  })

  it('hostile text in the preview stays inert', () => {
    setup('<script>alert(1)</script> [x](javascript:alert(1))')
    const preview = screen.getByRole('region', { name: 'Body preview' })
    expect(preview.querySelector('script, a')).toBeNull()
  })

  it('every toolbar button is a real, named button that does not submit a form', () => {
    setup('x')
    const toolbar = screen.getByRole('toolbar', { name: 'Format body' })
    for (const button of within(toolbar).getAllByRole('button')) {
      expect(button).toHaveAttribute('type', 'button')
      expect(button).toHaveAccessibleName()
    }
  })
})

describe('FormattedTextarea inside a react-hook-form form', () => {
  it('a toolbar edit alone marks the settings as changed (Save turns on) and is what gets saved', async () => {
    const { renderApp } = await import('../../test/renderApp')
    const { signedIn, API } = await import('../../test/fixtures')
    const { server } = await import('../../test/server')
    const { http, HttpResponse } = await import('msw')
    let saved: { key: string; value: string }[] | null = null
    server.use(
      http.put(`${API}/settings`, async ({ request }) => {
        saved = (await request.json()) as typeof saved
        return HttpResponse.json(saved)
      }),
    )
    const { user } = renderApp('/site/settings', signedIn)
    const story = (await screen.findByRole('heading', { name: 'Story' })).closest('div')!.parentElement!
    const box = within(story).getByLabelText('Body') as HTMLTextAreaElement
    expect(screen.getByRole('button', { name: 'Save settings' })).toBeDisabled()

    box.focus()
    box.setSelectionRange(0, 5)
    fireEvent.select(box)
    await user.click(within(story).getByRole('button', { name: 'Bold (Ctrl+B)' }))

    const save = screen.getByRole('button', { name: 'Save settings' })
    expect(save).toBeEnabled()
    await user.click(save)
    await screen.findByText('Settings saved.')
    expect(saved!.find((s) => s.key === 'story_body')!.value.startsWith('**')).toBe(true)
  })
})
