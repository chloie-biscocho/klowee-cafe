import { lazy, Suspense, useId, useRef, useState, type DragEvent } from 'react'
import { useUploadImageMutation } from '../../api/uploadsApi'
import { problemMessage } from '../../lib/errors'
import type { UploadFolder } from '../../types/api'
import { Button } from './Button'
import { Spinner } from './Spinner'
import type { CropRequest } from './CropDialog'

// The crop dialog and react-easy-crop load only when a dialog opens (handover 21).
const CropDialog = lazy(() => import('./CropDialog'))

const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp']
const MAX_BYTES = 5 * 1024 * 1024

/**
 * A courtesy check so an obviously wrong file fails instantly, without a
 * round trip. The API repeats it — plus the magic-byte check — and is the
 * authority (decision 009).
 */
function precheckImage(file: File): string | null {
  if (!ACCEPTED_TYPES.includes(file.type)) return 'Choose a JPEG, PNG or WebP image.'
  if (file.size > MAX_BYTES) return 'That image is over 5 MB. Choose a smaller one.'
  return null
}

interface ImageUploadProps {
  value: string | null
  onChange: (url: string | null) => void
  folder: UploadFolder
  label: string
  /** A smaller drop zone, for grids of photos. */
  compact?: boolean
  /**
   * The crop frame's shape: width ÷ height as the site shows this image, or
   * 'free' where the site fixes none (the owner picks a shape).
   */
  aspect?: number | 'free'
  /** The site shows it in a circle: a square frame with a round guide. */
  round?: boolean
  /** A QR code: "Use original" is the default, and nothing is cropped unless the frame was moved. */
  keepOriginal?: boolean
}

/**
 * Click or drop an image; after the pre-check a crop dialog opens (handover
 * 21): Use original, or Crop and upload, and `onChange` receives the public
 * URL. Cancel keeps the previous image. An existing image can be cropped again
 * (Crop), which uploads the result as a new file. Remove only clears the field;
 * the stored object stays in the bucket (orphan cleanup is a known gap,
 * decision 009).
 */
export function ImageUpload({ value, onChange, folder, label, compact = false, aspect = 'free', round = false, keepOriginal = false }: ImageUploadProps) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [upload, { isLoading }] = useUploadImageMutation()
  const [error, setError] = useState<string | null>(null)
  const [isDragOver, setDragOver] = useState(false)
  const [cropping, setCropping] = useState<CropRequest | null>(null)
  const [opening, setOpening] = useState(false)

  const closeCrop = () => {
    if (cropping) URL.revokeObjectURL(cropping.src)
    setCropping(null)
  }

  /** A chosen or dropped file: the pre-check, then the crop dialog. */
  const accept = (file: File | undefined) => {
    if (!file) return
    const problem = precheckImage(file)
    if (problem) {
      setError(problem)
      return
    }
    setError(null)
    setCropping({ src: URL.createObjectURL(file), file })
  }

  /** Crop the stored image: fetched as a blob, so the canvas can read it (storage allows any origin). */
  const recrop = async () => {
    if (!value) return
    setOpening(true)
    setError(null)
    try {
      const response = await fetch(value)
      if (!response.ok) throw new Error(String(response.status))
      const blob = await response.blob()
      const name = decodeURIComponent(value.split('/').pop() ?? 'image')
      const file = new File([blob], name, { type: blob.type || 'image/webp' })
      setCropping({ src: URL.createObjectURL(file), file, recrop: true })
    } catch {
      setError("The stored image couldn't be opened for cropping. Try again, or upload the photo again.")
    } finally {
      setOpening(false)
    }
  }

  const send = async (file: File) => {
    closeCrop()
    try {
      const result = await upload({ file, folder }).unwrap()
      onChange(result.url)
    } catch (uploadError) {
      // The previous value stays: a failed upload never clears a good photo.
      setError(problemMessage(uploadError as Parameters<typeof problemMessage>[0]))
    }
  }

  const onDrop = (event: DragEvent) => {
    event.preventDefault()
    setDragOver(false)
    if (!isLoading) accept(event.dataTransfer.files[0])
  }

  const height = compact ? 'h-28' : 'h-36'

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[0.8rem] font-bold text-muted">
        {label}
      </label>
      <input
        ref={inputRef}
        id={id}
        type="file"
        accept={ACCEPTED_TYPES.join(',')}
        className="sr-only"
        onChange={(event) => {
          accept(event.target.files?.[0])
          // Lets the owner pick the same file again after an error.
          event.target.value = ''
        }}
      />

      {value ? (
        <div className="flex items-end gap-3">
          <img
            src={value}
            alt={label}
            className={`${height} max-w-full rounded-2xl border border-line object-cover`}
          />
          <div className="flex flex-col gap-1.5">
            <Button size="sm" loading={isLoading} onClick={() => inputRef.current?.click()}>
              Replace
            </Button>
            <div className="flex gap-1.5">
              <Button size="sm" loading={opening} disabled={isLoading} onClick={() => void recrop()}>
                Crop
              </Button>
              <Button size="sm" variant="danger" disabled={isLoading} onClick={() => onChange(null)}>
                Remove
              </Button>
            </div>
          </div>
        </div>
      ) : (
        <button
          type="button"
          disabled={isLoading}
          onClick={() => inputRef.current?.click()}
          onDragOver={(event) => {
            event.preventDefault()
            setDragOver(true)
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={onDrop}
          className={`relative flex ${height} w-full flex-col items-center justify-center gap-1 rounded-2xl border-2 border-dashed text-sm transition-colors ${
            isDragOver ? 'border-accent bg-accent-soft text-accent' : 'border-line text-muted hover:bg-canvas'
          }`}
        >
          {isLoading ? (
            <Spinner className="size-5" label="Uploading" />
          ) : (
            <>
              <span className="font-medium">Drop an image or click to choose</span>
              <span className="text-xs">JPEG, PNG or WebP, up to 5 MB</span>
            </>
          )}
        </button>
      )}

      {error && (
        <p role="alert" className="text-xs text-danger">
          {error}
        </p>
      )}

      {cropping && (
        <Suspense fallback={null}>
          <CropDialog request={cropping} aspect={aspect} round={round} keepOriginal={keepOriginal} onCancel={closeCrop} onDone={(file) => void send(file)} />
        </Suspense>
      )}
    </div>
  )
}
