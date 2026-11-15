/// <reference types="node" />
import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { File as NodeFile } from 'node:buffer'
import { http, HttpResponse } from 'msw'
import { useEffect } from 'react'
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderWithStore } from '../../test/renderApp'
import { problem, server } from '../../test/server'
import { cropToFile, exportSize, rotatedSize, exportType } from './cropImage'
import { ImageUpload } from './ImageUpload'
import { Modal } from './Modal'

const STORED = 'https://example.supabase.co/storage/v1/object/public/media/events/2026/05/a.png'

/**
 * jsdom never loads an image and has no canvas, so the cropper reports a
 * 1600 × 1200 photo and the largest frame of the asked-for shape (as
 * react-easy-crop does at first), and the canvas step is replaced to record
 * the area it would draw. The real drawing is checked in Chrome (handover 21
 * report). Moving the frame is a keyboard press on the stand-in.
 */
vi.mock('react-easy-crop', () => ({
  default: function FakeCropper(props: {
    aspect: number
    onMediaLoaded?: (size: { naturalWidth: number; naturalHeight: number; width: number; height: number }) => void
    onCropComplete?: (area: object, pixels: { x: number; y: number; width: number; height: number }) => void
    onInteractionStart?: () => void
    cropperProps?: Record<string, string>
  }) {
    const { aspect } = props
    // Keyed on the frame's shape only: the dialog passes new callbacks on every render.
    useEffect(() => {
      props.onMediaLoaded?.({ naturalWidth: 1600, naturalHeight: 1200, width: 1600, height: 1200 })
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [])
    useEffect(() => {
      const width = Math.min(1600, 1200 * aspect)
      const height = width / aspect
      props.onCropComplete?.({}, { x: (1600 - width) / 2, y: (1200 - height) / 2, width, height })
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [aspect])
    return <div data-testid="cropper" tabIndex={0} onKeyDown={() => props.onInteractionStart?.()} {...props.cropperProps} />
  },
}))
vi.mock('./cropImage', async (original) => {
  const actual = await original<typeof import('./cropImage')>()
  return {
    ...actual,
    cropToFile: vi.fn(async (_src: string, area: { width: number; height: number }, _rotation: number, name: string) => {
      const size = actual.exportSize(area)
      return new File([new Uint8Array(32)], `${name.replace(/\.[^.]+$/, '')}-cropped-${size.width}x${size.height}.png`, { type: 'image/png' })
    }),
  }
})

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
  URL.createObjectURL = vi.fn(() => 'blob:test-image')
  URL.revokeObjectURL = vi.fn()
})
afterAll(() => vi.unstubAllGlobals())
// Only the call history is reset: in this Vitest, mockClear() on this mock ran its implementation with no arguments.
beforeEach(() => {
  vi.mocked(cropToFile).mock.calls.length = 0
})

function pngFile() {
  return new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'cover.png', { type: 'image/png' })
}

/** Records what reaches POST /uploads. */
function serveUpload() {
  const sent: File[] = []
  server.use(
    http.post(`${API}/uploads`, async ({ request }) => {
      sent.push((await request.formData()).get('file') as File)
      return HttpResponse.json({ url: STORED, path: 'events/2026/05/a.png', contentType: 'image/png', size: 4 })
    }),
  )
  return sent
}

describe('ImageUpload', () => {
  it('opens the crop dialog after choosing a file; Use original posts the file as chosen', async () => {
    let folder: string | null = null
    const sent: File[] = []
    server.use(
      http.post(`${API}/uploads`, async ({ request }) => {
        folder = new URL(request.url).searchParams.get('folder')
        sent.push((await request.formData()).get('file') as File)
        return HttpResponse.json({ url: STORED, path: 'events/2026/05/a.png', contentType: 'image/png', size: 4 })
      }),
    )
    const onChange = vi.fn()
    const { user } = renderWithStore(<ImageUpload value={null} onChange={onChange} folder="events" label="Cover image" aspect={4 / 3} />, signedIn)

    await user.upload(screen.getByLabelText('Cover image'), pngFile())
    const dialog = await screen.findByRole('dialog', { name: 'Frame the photo' })
    expect(sent).toHaveLength(0)

    await user.click(within(dialog).getByRole('button', { name: 'Use original' }))
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(STORED))
    expect(folder).toBe('events')
    expect(sent[0]!.name).toBe('cover.png')
    expect(sent[0]!.type).toBe('image/png')
    expect(sent[0]!.size).toBe(4)
    expect(cropToFile).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('Crop and upload posts a different, cropped file in the frame proportions (4:3 for an event cover)', async () => {
    const sent = serveUpload()
    const onChange = vi.fn()
    const { user } = renderWithStore(<ImageUpload value={null} onChange={onChange} folder="events" label="Cover image" aspect={4 / 3} />, signedIn)

    await user.upload(screen.getByLabelText('Cover image'), pngFile())
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Crop and upload' }))

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(STORED))
    const [, area] = vi.mocked(cropToFile).mock.calls[0]!
    expect(area.width / area.height).toBeCloseTo(4 / 3)
    expect(sent[0]!.name).toBe('cover-cropped-1600x1200.png')
    expect(sent[0]!.name).not.toBe('cover.png')
  })

  it('a round story frame is square', async () => {
    serveUpload()
    const { user } = renderWithStore(<ImageUpload value={null} onChange={vi.fn()} folder="site" label="Story image" aspect={1} round />, signedIn)

    await user.upload(screen.getByLabelText('Story image'), pngFile())
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Crop and upload' }))

    await waitFor(() => expect(cropToFile).toHaveBeenCalled())
    const [, area] = vi.mocked(cropToFile).mock.calls[0]!
    expect(area.width).toBe(area.height)
  })

  it('Cancel posts nothing and keeps the previous image', async () => {
    const sent = serveUpload()
    const onChange = vi.fn()
    const { user } = renderWithStore(<ImageUpload value={STORED} onChange={onChange} folder="events" label="Cover image" aspect={4 / 3} />, signedIn)

    await user.upload(screen.getByLabelText('Cover image'), pngFile())
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(sent).toHaveLength(0)
    expect(onChange).not.toHaveBeenCalled()
    expect(screen.getByRole('img', { name: 'Cover image' })).toHaveAttribute('src', STORED)
  })

  it("a QR code's default is the original: Use original is the main button, and Crop sends the original unless the frame moved", async () => {
    const sent = serveUpload()
    const { user } = renderWithStore(<ImageUpload value={null} onChange={vi.fn()} folder="site" label="QR code" keepOriginal />, signedIn)

    await user.upload(screen.getByLabelText('QR code'), pngFile())
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: 'Use original' }).className).toContain('bg-accent')

    await user.click(within(dialog).getByRole('button', { name: 'Crop and upload' }))
    await waitFor(() => expect(sent).toHaveLength(1))
    expect(sent[0]!.name).toBe('cover.png')
    expect(cropToFile).not.toHaveBeenCalled()

    // Moved, it really crops (pixel for pixel: smoothing off).
    await user.upload(screen.getByLabelText('QR code'), pngFile())
    const again = await screen.findByRole('dialog')
    fireEvent.keyDown(within(again).getByTestId('cropper'), { key: 'ArrowLeft' })
    await user.click(within(again).getByRole('button', { name: 'Crop and upload' }))
    await waitFor(() => expect(cropToFile).toHaveBeenCalledTimes(1))
    expect(vi.mocked(cropToFile).mock.calls[0]![5]).toBe(true)
  })

  it('Crop on an existing image opens the dialog on the stored image, and uploads the result as a new file', async () => {
    server.use(http.get(STORED, () => new HttpResponse(new Uint8Array([0x89, 0x50, 0x4e, 0x47]), { headers: { 'content-type': 'image/png' } })))
    const sent = serveUpload()
    const onChange = vi.fn()
    const { user } = renderWithStore(<ImageUpload value={STORED} onChange={onChange} folder="events" label="Cover image" aspect={4 / 3} />, signedIn)

    await user.click(screen.getByRole('button', { name: 'Crop' }))
    const dialog = await screen.findByRole('dialog', { name: 'Crop the photo' })
    // Re-cropping has no "Use original": that would be Cancel.
    expect(within(dialog).queryByRole('button', { name: 'Use original' })).not.toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Crop and upload' }))
    await waitFor(() => expect(onChange).toHaveBeenCalledWith(STORED))
    expect(sent[0]!.name).toBe('a-cropped-1600x1200.png')
  })

  it('Escape closes the crop dialog only, not the form underneath', async () => {
    const { user } = renderWithStore(
      <Modal open title="New event" onClose={vi.fn(() => {})}>
        <ImageUpload value={null} onChange={vi.fn()} folder="events" label="Cover image" aspect={4 / 3} />
      </Modal>,
      signedIn,
    )
    await user.upload(screen.getByLabelText('Cover image'), pngFile())
    await screen.findByRole('dialog', { name: 'Frame the photo' })

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog', { name: 'Frame the photo' })).not.toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'New event' })).toBeInTheDocument()
  })

  it('the crop dialog names every control, and + and − zoom', async () => {
    const { user } = renderWithStore(<ImageUpload value={null} onChange={vi.fn()} folder="menu" label="Photo" />, signedIn)
    await user.upload(screen.getByLabelText('Photo'), pngFile())
    const dialog = await screen.findByRole('dialog')

    const zoom = within(dialog).getByRole('slider', { name: 'Zoom' })
    expect(zoom).toHaveValue('1')
    within(dialog).getByTestId('cropper').focus()
    await user.keyboard('+')
    expect(zoom).toHaveValue('1.1')
    await user.keyboard('-')
    expect(zoom).toHaveValue('1')
    expect(within(dialog).getByRole('group', { name: 'Frame. Arrow keys move the photo; + and − zoom.' })).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Turn left 90 degrees' })).toBeInTheDocument()
    // A free frame (the menu photo isn't on the site yet): the owner picks a shape.
    expect(within(within(dialog).getByRole('group', { name: 'Frame shape' })).getByRole('button', { name: 'Original shape', pressed: true })).toBeInTheDocument()
  })

  it("shows the API's 400 inline and keeps the previous value", async () => {
    const message = "The file's contents don't match its declared type 'image/png'."
    server.use(http.post(`${API}/uploads`, () => problem(400, 'Validation failed', message)))
    const onChange = vi.fn()
    const { user } = renderWithStore(<ImageUpload value={null} onChange={onChange} folder="events" label="Cover image" />, signedIn)

    await user.upload(screen.getByLabelText('Cover image'), pngFile())
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Use original' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
    expect(onChange).not.toHaveBeenCalled()
  })

  it('stops a non-image before it reaches the API', async () => {
    const onChange = vi.fn()
    renderWithStore(<ImageUpload value={null} onChange={onChange} folder="menu" label="Photo" />, signedIn)

    // A drop skips the file input's `accept` filter, so this is the real path
    // by which a wrong file arrives. No upload handler is registered: a request
    // would fail the test as unhandled.
    fireEvent.drop(screen.getByRole('button', { name: /drop an image/i }), {
      dataTransfer: { files: [new File(['hello'], 'notes.txt', { type: 'text/plain' })] },
    })

    expect(await screen.findByRole('alert')).toHaveTextContent('Choose a JPEG, PNG or WebP image.')
    expect(onChange).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})

describe('the crop maths', () => {
  it('exports the area itself, never enlarged, with the long edge at most 2400px', () => {
    expect(exportSize({ width: 4000, height: 3000 })).toEqual({ width: 2400, height: 1800 })
    expect(exportSize({ width: 1200, height: 900 })).toEqual({ width: 1200, height: 900 })
    expect(exportSize({ width: 1000, height: 5000 })).toEqual({ width: 480, height: 2400 })
  })

  it('a quarter turn swaps the sides', () => {
    expect(rotatedSize(1600, 1200, 90)).toEqual({ width: 1200, height: 1600 })
    expect(rotatedSize(1600, 1200, 180)).toEqual({ width: 1600, height: 1200 })
    expect(rotatedSize(1600, 1200, -90)).toEqual({ width: 1200, height: 1600 })
  })

  it('PNG stays PNG; photos become JPEG', () => {
    expect(exportType('image/png')).toBe('image/png')
    expect(exportType('image/jpeg')).toBe('image/jpeg')
    expect(exportType('image/webp')).toBe('image/jpeg')
  })
})
