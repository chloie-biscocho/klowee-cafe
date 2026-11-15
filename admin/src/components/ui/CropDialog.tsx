import { useEffect, useId, useState, type KeyboardEvent } from 'react'
import Cropper, { type Area, type MediaSize } from 'react-easy-crop'
import { Button } from './Button'
import { cropToFile, FREE_SHAPES } from './cropImage'

export interface CropRequest {
  /** An object URL of the image to crop. */
  src: string
  /** The file as chosen (or the stored image, for a re-crop). */
  file: File
  /** A re-crop of the stored image: no "Use original" (that's just Cancel). */
  recrop?: boolean
}

export interface CropDialogProps {
  request: CropRequest
  /** The frame's shape as the site shows the image, or 'free' to let the owner pick one. */
  aspect: number | 'free'
  /** A round guide over a square frame, where the site shows the image in a circle. */
  round?: boolean
  /** A QR code: "Use original" is the default, and unless the frame was moved, the original is what goes up. */
  keepOriginal?: boolean
  onCancel: () => void
  /** The file to upload: the original, or the cropped one. */
  onDone: (file: File) => void
}

const ZOOM_MIN = 1
const ZOOM_MAX = 4
const ZOOM_STEP = 0.1
/** Below this long edge, a re-crop of the stored copy says it is small. */
const SMALL_EDGE = 1200

/**
 * The crop dialog (handover 21), its own chunk with react-easy-crop: loaded
 * only when it opens. Drag, the slider, the wheel or a pinch to zoom; buttons
 * to turn by 90 degrees; arrow keys move the frame, + and − zoom. Escape
 * closes this dialog only, never the form underneath.
 */
export default function CropDialog({ request, aspect, round = false, keepOriginal = false, onCancel, onDone }: CropDialogProps) {
  const id = useId()
  const [crop, setCrop] = useState({ x: 0, y: 0 })
  const [zoom, setZoom] = useState(1)
  const [rotation, setRotation] = useState(0)
  const [area, setArea] = useState<Area | null>(null)
  const [media, setMedia] = useState<MediaSize | null>(null)
  const [shape, setShape] = useState<number | 'original'>(keepOriginal ? 'original' : aspect === 'free' ? 'original' : aspect)
  const [moved, setMoved] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const frameAspect =
    shape === 'original' ? (media ? (rotation % 180 === 0 ? media.naturalWidth / media.naturalHeight : media.naturalHeight / media.naturalWidth) : 1) : shape

  // Escape closes the crop dialog only: caught first, so a form modal under it stays open.
  useEffect(() => {
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopImmediatePropagation()
      event.preventDefault()
      onCancel()
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [onCancel])

  const zoomBy = (delta: number) => {
    setMoved(true)
    setZoom((current) => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, Math.round((current + delta) * 10) / 10)))
  }
  const turn = (by: number) => {
    setMoved(true)
    setRotation((current) => (current + by + 360) % 360)
  }

  const keys = (event: KeyboardEvent) => {
    if (event.key === '+' || event.key === '=') {
      event.preventDefault()
      zoomBy(ZOOM_STEP)
    } else if (event.key === '-' || event.key === '_') {
      event.preventDefault()
      zoomBy(-ZOOM_STEP)
    }
  }

  const sendOriginal = () => onDone(request.file)
  const cropAndUpload = async () => {
    // A QR code goes up as chosen unless the owner actually moved the frame.
    if (keepOriginal && !moved) {
      sendOriginal()
      return
    }
    if (!area) return
    setBusy(true)
    setError(null)
    try {
      onDone(await cropToFile(request.src, area, rotation, request.file.name, request.file.type, keepOriginal))
    } catch (cropError) {
      setError(cropError instanceof Error ? cropError.message : 'The image could not be cropped.')
      setBusy(false)
    }
  }

  const small = request.recrop && media && Math.max(media.naturalWidth, media.naturalHeight) < SMALL_EDGE

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-scrim p-4 sm:p-8">
      <div role="dialog" aria-modal="true" aria-labelledby={`${id}-title`} className="w-full max-w-2xl rounded-[18px] bg-surface shadow-lg" onKeyDown={keys}>
        <div className="border-b-[1.5px] border-line px-5 py-3.5">
          <h2 id={`${id}-title`} className="font-serif text-lg text-accent">
            {request.recrop ? 'Crop the photo' : 'Frame the photo'}
          </h2>
          <p id={`${id}-help`} className="mt-1 text-xs text-muted">
            Drag to move. Zoom with the slider, the scroll wheel or a pinch. Arrow keys move the frame; + and − zoom.
          </p>
        </div>

        <div className="flex flex-col gap-3 px-5 py-4">
          {small && (
            <p className="rounded-2xl bg-warning-soft px-3 py-2 text-xs text-warning">
              This is the stored copy, {media.naturalWidth} × {media.naturalHeight} pixels. Cropping it makes it smaller still; for a sharper result, upload the original photo again.
            </p>
          )}

          <div className="relative h-[min(60vh,420px)] overflow-hidden rounded-2xl bg-card-ink">
            <Cropper
              image={request.src}
              crop={crop}
              zoom={zoom}
              rotation={rotation}
              aspect={frameAspect}
              cropShape={round ? 'round' : 'rect'}
              showGrid={!round}
              minZoom={ZOOM_MIN}
              maxZoom={ZOOM_MAX}
              zoomSpeed={0.5}
              keyboardStep={8}
              onCropChange={setCrop}
              onZoomChange={setZoom}
              onRotationChange={setRotation}
              onCropComplete={(_, pixels) => setArea(pixels)}
              onMediaLoaded={setMedia}
              onInteractionStart={() => setMoved(true)}
              cropperProps={{ 'aria-label': 'Frame. Arrow keys move the photo; + and − zoom.', 'aria-describedby': `${id}-help`, role: 'group' }}
              mediaProps={{ alt: 'The photo being framed' }}
            />
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <label htmlFor={`${id}-zoom`} className="text-[0.8rem] font-bold text-muted">
              Zoom
            </label>
            <input
              id={`${id}-zoom`}
              type="range"
              min={ZOOM_MIN}
              max={ZOOM_MAX}
              step={ZOOM_STEP}
              value={zoom}
              onChange={(event) => {
                setMoved(true)
                setZoom(Number(event.target.value))
              }}
              className="min-w-40 flex-1 accent-accent"
            />
            <Button size="sm" aria-label="Turn left 90 degrees" onClick={() => turn(-90)}>
              ⟲ Turn left
            </Button>
            <Button size="sm" aria-label="Turn right 90 degrees" onClick={() => turn(90)}>
              Turn right ⟳
            </Button>
          </div>

          {aspect === 'free' && (
            <div role="group" aria-label="Frame shape" className="flex flex-wrap gap-1.5">
              {FREE_SHAPES.map((option) => (
                <Button
                  key={option.label}
                  size="sm"
                  variant={shape === option.aspect ? 'primary' : 'secondary'}
                  aria-pressed={shape === option.aspect}
                  onClick={() => {
                    setMoved(true)
                    setShape(option.aspect)
                  }}
                >
                  {option.label}
                </Button>
              ))}
            </div>
          )}

          {error && (
            <p role="alert" className="text-xs text-danger">
              {error}
            </p>
          )}
        </div>

        <div className="flex flex-wrap justify-end gap-2 border-t-[1.5px] border-line px-5 py-3.5">
          <Button onClick={onCancel} disabled={busy}>
            Cancel
          </Button>
          {!request.recrop && (
            <Button variant={keepOriginal ? 'primary' : 'secondary'} onClick={sendOriginal} disabled={busy}>
              Use original
            </Button>
          )}
          <Button variant={keepOriginal ? 'secondary' : 'primary'} loading={busy} onClick={() => void cropAndUpload()}>
            Crop and upload
          </Button>
        </div>
      </div>
    </div>
  )
}
