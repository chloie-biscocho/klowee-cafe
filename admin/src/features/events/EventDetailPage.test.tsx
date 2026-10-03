import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { EventPhotoRequest } from '../../types/api'
import { API, signedIn, upcomingEvent } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { problem, server } from '../../test/server'

const route = `/site/events/${upcomingEvent.id}`

describe('EventDetailPage', () => {
  it('disables Publish for a Planned event and says why', async () => {
    server.use(
      http.get(`${API}/events/:id`, () => HttpResponse.json({ ...upcomingEvent, status: 'Planned' })),
    )

    renderApp(route, signedIn)

    const publish = await screen.findByRole('button', { name: 'Publish' })
    expect(publish).toBeDisabled()
    expect(publish.parentElement).toHaveAttribute(
      'title',
      expect.stringContaining("Planned and Cancelled events can't be published"),
    )
  })

  it("shows the API's 409 if a publish gets through anyway", async () => {
    const conflict =
      "Planned and Cancelled events can't be published; set the status to Upcoming or Done first."
    server.use(http.patch(`${API}/events/:id/publish`, () => problem(409, 'Conflict', conflict)))

    const { user } = renderApp(route, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Publish' }))

    expect(await screen.findByText(conflict)).toBeInTheDocument()
  })

  it('saves the reordered photo list in full, with sortOrder from the new positions', async () => {
    let sent: EventPhotoRequest[] | null = null
    server.use(
      http.put(`${API}/events/:id/photos`, async ({ request }) => {
        sent = (await request.json()) as EventPhotoRequest[]
        return HttpResponse.json(upcomingEvent)
      }),
    )

    const { user } = renderApp(route, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Move photo 2 earlier' }))
    await user.type(screen.getByLabelText('Caption for photo 1'), 'Opening night')
    await user.click(screen.getByRole('button', { name: 'Save photos' }))

    await waitFor(() => expect(sent).not.toBeNull())
    expect(sent).toEqual([
      { url: upcomingEvent.photos[1]!.url, caption: 'Opening night', sortOrder: 0 },
      { url: upcomingEvent.photos[0]!.url, caption: 'Setting up', sortOrder: 1 },
    ])
    expect(await screen.findByText('Photos saved.')).toBeInTheDocument()
  })
})
