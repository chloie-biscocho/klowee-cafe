import { useId, useRef, useState, type DragEvent } from 'react'
import { useUploadImageMutation } from '../../api/uploadsApi'
import { problemMessage } from '../../lib/errors'
import type { UploadFolder } from '../../types/api'
import { Button } from './Button'
import { Spinner } from './Spinner'

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
}

/**
 * Click or drop an image; it uploads straight away and `onChange` receives the
 * public URL. Remove only clears the field — the stored object stays in the
 * bucket (orphan cleanup is a known gap, decision 009).
 */
export function ImageUpload({ value, onChange, folder, label, compact = false }: ImageUploadProps) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [upload, { isLoading }] = useUploadImageMutation()
  const [error, setError] = useState<string | null>(null)
  const [isDragOver, setDragOver] = useState(false)

  const accept = async (file: File | undefined) => {
    if (!file) return
    const problem = precheckImage(file)
    if (problem) {
      setError(problem)
      return
    }
    setError(null)
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
    if (!isLoading) void accept(event.dataTransfer.files[0])
  }

  const height = compact ? 'h-28' : 'h-36'

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-xs font-semibold text-muted">
        {label}
      </label>
      <input
        ref={inputRef}
        id={id}
        type="file"
        accept={ACCEPTED_TYPES.join(',')}
        className="sr-only"
        onChange={(event) => {
          void accept(event.target.files?.[0])
          // Lets the owner pick the same file again after an error.
          event.target.value = ''
        }}
      />

      {value ? (
        <div className="flex items-end gap-3">
          <img
            src={value}
            alt={label}
            className={`${height} max-w-full rounded-md border border-line object-cover`}
          />
          <div className="flex flex-col gap-1.5">
            <Button size="sm" loading={isLoading} onClick={() => inputRef.current?.click()}>
              Replace
            </Button>
            <Button size="sm" variant="danger" disabled={isLoading} onClick={() => onChange(null)}>
              Remove
            </Button>
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
          className={`relative flex ${height} w-full flex-col items-center justify-center gap-1 rounded-md border-2 border-dashed text-sm transition-colors ${
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
    </div>
  )
}
