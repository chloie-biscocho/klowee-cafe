import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { draftVersion, publishedVersion, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'

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
})
