/**
 * Cropping before upload (handover 21, decision 024). The maths is pure and
 * tested; `cropToFile` draws the chosen area to a canvas and hands back a file
 * for the usual upload. The server still resizes to 1600px and re-encodes as
 * WebP (decision 012), so this exports large and clean: that step is the only
 * real compression.
 */

export interface Area {
  x: number
  y: number
  width: number
  height: number
}

/** The long edge the export aims for: above the server's 1600px, so its resize does the final step. */
export const MAX_EXPORT_EDGE = 2400
/** The upload limit (decision 009). */
export const MAX_BYTES = 5 * 1024 * 1024

/** The output size for a crop: the area itself, shrunk (never enlarged) so its long edge is at most `maxEdge`. */
export function exportSize(area: Pick<Area, 'width' | 'height'>, maxEdge = MAX_EXPORT_EDGE): { width: number; height: number } {
  const scale = Math.min(1, maxEdge / Math.max(area.width, area.height))
  return { width: Math.max(1, Math.round(area.width * scale)), height: Math.max(1, Math.round(area.height * scale)) }
}

/** The bounding box of an image turned by a multiple of 90 degrees. */
export function rotatedSize(width: number, height: number, rotation: number): { width: number; height: number } {
  const quarter = ((Math.round(rotation / 90) % 4) + 4) % 4
  return quarter % 2 === 1 ? { width: height, height: width } : { width, height }
}

/** PNG stays PNG (QR codes, graphics with sharp edges); photos go out as high-quality JPEG. */
export function exportType(inputType: string): 'image/png' | 'image/jpeg' {
  return inputType === 'image/png' ? 'image/png' : 'image/jpeg'
}

/** The frame shapes offered when the site doesn't fix one: the photo's own, or a common ratio. */
export const FREE_SHAPES: { label: string; aspect: number | 'original' }[] = [
  { label: 'Original shape', aspect: 'original' },
  { label: 'Square', aspect: 1 },
  { label: '4:3', aspect: 4 / 3 },
  { label: '3:4', aspect: 3 / 4 },
  { label: '16:9', aspect: 16 / 9 },
]

function loadImage(src: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image()
    image.onload = () => resolve(image)
    image.onerror = () => reject(new Error('The image could not be read.'))
    image.src = src
  })
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality))
}

/**
 * Draws `area` of the image (in the rotated image's pixels, as the cropper
 * reports it) to a canvas at `exportSize`, and returns it as a file under
 * 5 MB: JPEG steps its quality down from 0.92, PNG its size, until it fits.
 * `crisp` turns smoothing off, for a QR code.
 */
export async function cropToFile(src: string, area: Area, rotation: number, name: string, inputType: string, crisp = false): Promise<File> {
  const image = await loadImage(src)
  const turned = rotatedSize(image.naturalWidth, image.naturalHeight, rotation)
  const type = exportType(inputType)
  let size = exportSize(area)
  let quality = 0.92

  for (let attempt = 0; attempt < 8; attempt++) {
    const canvas = document.createElement('canvas')
    canvas.width = size.width
    canvas.height = size.height
    const context = canvas.getContext('2d')
    if (!context) throw new Error('This browser cannot crop images.')
    context.imageSmoothingEnabled = !crisp
    context.imageSmoothingQuality = 'high'
    if (type === 'image/jpeg') {
      // JPEG has no transparency: a transparent PNG area becomes white, not black.
      context.fillStyle = '#ffffff'
      context.fillRect(0, 0, size.width, size.height)
    }
    // One transform: scale the area to the output, move it to the origin, then turn the image about its centre.
    context.scale(size.width / area.width, size.height / area.height)
    context.translate(-area.x, -area.y)
    context.translate(turned.width / 2, turned.height / 2)
    context.rotate((rotation * Math.PI) / 180)
    context.drawImage(image, -image.naturalWidth / 2, -image.naturalHeight / 2)

    const blob = await toBlob(canvas, type, type === 'image/jpeg' ? quality : undefined)
    if (!blob) throw new Error('The cropped image could not be made.')
    if (blob.size <= MAX_BYTES) {
      const extension = blob.type === 'image/png' ? 'png' : 'jpg'
      return new File([blob], `${name.replace(/\.[^.]+$/, '') || 'image'}-cropped.${extension}`, { type: blob.type })
    }
    if (type === 'image/jpeg' && quality > 0.7) quality -= 0.07
    else size = { width: Math.round(size.width * 0.85), height: Math.round(size.height * 0.85) }
  }
  throw new Error('The cropped image is still over 5 MB. Try a smaller frame.')
}
