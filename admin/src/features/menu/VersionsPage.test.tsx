import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { draftVersion, publishedVersion, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { MENU_CONTEXT_LABELS } from '../../types/api'

describe('VersionsPage', () => {
  it('renders a row per version and marks the published one', async () => {
    renderApp('/menu/versions', signedIn)

    const draftRow = (await screen.findByText(draftVersion.name)).closest('tr')!
    const publishedRow = (await screen.findByText(publishedVersion.name)).closest('tr')!

    expect(within(draftRow).getByText('Draft')).toBeInTheDocument()
    expect(within(draftRow).getByText('May 1, 2026')).toBeInTheDocument()
    expect(within(draftRow).getByText('2')).toBeInTheDocument()

    expect(within(publishedRow).getByText('Published')).toBeInTheDocument()
    // A published version can only be unpublished, never edited or deleted.
    expect(within(publishedRow).getByRole('button', { name: 'Unpublish' })).toBeInTheDocument()
    expect(within(publishedRow).queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(within(publishedRow).queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument()
  })

  it('offers versions of every context to copy from, grouped, so an office menu can start from a pop-up one', async () => {
    const { user } = renderApp('/menu/versions', signedIn)

    await user.click((await screen.findAllByRole('button', { name: 'New version' }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.selectOptions(within(dialog).getByLabelText('Context'), 'Office')

    const copyFrom = within(dialog).getByLabelText('Copy items from')
    const group = copyFrom.querySelector('optgroup')!
    expect(group.label).toBe(MENU_CONTEXT_LABELS.PopUp)
    expect(within(copyFrom).getByRole('option', { name: /May 2026 pop-up/ })).toBeInTheDocument()
  })
})
