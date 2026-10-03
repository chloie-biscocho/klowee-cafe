/// <reference types="node" />
import { fireEvent, screen, waitFor } from '@testing-library/react'
import { File as NodeFile } from 'node:buffer'
import { http, HttpResponse } from 'msw'
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderWithStore } from '../../test/renderApp'
import { problem, server } from '../../test/server'
import { ImageUpload } from './ImageUpload'

const STORED = 'https://example.supabase.co/storage/v1/object/public/media/events/2026/05/a.png'

/**
 * Under Vitest's jsdom environment `FormData` and `File` are jsdom's while
 * `fetch` is Node's, and Node's fetch cannot serialise a jsdom FormData — the
 * file arrives as an empty part named "blob". A browser has one implementation
 * of each, so this is purely a test-environment gap: swap Node's natives in for
 * this file. (`Response#formData()` is the one place Node hands its FormData out.)
 */
const NativeFormData = (await new Response(new URLSearchParams('a=1')).formData()).constructor
beforeAll(() => {
  vi.stubGlobal('FormData', NativeFormData)
  vi.stubGlobal('File', NodeFile)
})
afterAll(() => vi.unstubAllGlobals())

function pngFile() {
  return new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'cover.png', { type: 'image/png' })
}

describe('ImageUpload', () => {
  it('posts the file as multipart and hands the returned URL to onChange', async () => {
    let folder: string | null = null
    let sent: File | null = null

    server.use(
      http.post(`${API}/uploads`, async ({ request }) => {
        folder = new URL(request.url).searchParams.get('folder')
        sent = (await request.formData()).get('file') as File
        return HttpResponse.json({ url: STORED, path: 'events/2026/05/a.png', contentType: 'image/png', size: 4 })
      }),
    )

    const onChange = vi.fn()
    const { user } = renderWithStore(
      <ImageUpload value={null} onChange={onChange} folder="events" label="Cover image" />,
      signedIn,
    )

    await user.upload(screen.getByLabelText('Cover image'), pngFile())

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(STORED))
    expect(folder).toBe('events')
    expect(sent!.name).toBe('cover.png')
    expect(sent!.type).toBe('image/png')
    expect(sent!.size).toBe(4)
  })

  it("shows the API's 400 inline and keeps the previous value", async () => {
    const message = "The file's contents don't match its declared type 'image/png'."
    server.use(http.post(`${API}/uploads`, () => problem(400, 'Validation failed', message)))

    const onChange = vi.fn()
    const { user } = renderWithStore(
      <ImageUpload value={null} onChange={onChange} folder="events" label="Cover image" />,
      signedIn,
    )

    await user.upload(screen.getByLabelText('Cover image'), pngFile())

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expect(onChange).not.toHaveBeenCalled()
  })

  it('stops a non-image before it reaches the API', async () => {
    const onChange = vi.fn()
    renderWithStore(
      <ImageUpload value={null} onChange={onChange} folder="menu" label="Photo" />,
      signedIn,
    )

    // A drop skips the file input's `accept` filter, so this is the real path
    // by which a wrong file arrives. No upload handler is registered: a request
    // would fail the test as unhandled.
    fireEvent.drop(screen.getByRole('button', { name: /drop an image/i }), {
      dataTransfer: { files: [new File(['hello'], 'notes.txt', { type: 'text/plain' })] },
    })

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a JPEG, PNG or WebP image.')
    expect(onChange).not.toHaveBeenCalled()
  })
})
