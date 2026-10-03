import { ASPECTS, type Aspect } from '../lib/aspects'
import { Placeholder } from './Placeholder'

/** A real image when there is a URL, the placeholder when there is not. */
export function Photo({
  url,
  alt,
  placeholder,
  aspect,
  className = '',
}: {
  url: string | null | undefined
  alt: string
  /** What the placeholder says belongs here. */
  placeholder: string
  aspect: Aspect
  className?: string
}) {
  if (!url) return <Placeholder caption={placeholder} aspect={aspect} className={className} />

  return (
    <img
      src={url}
      alt={alt}
      loading="lazy"
      decoding="async"
      className={`w-full rounded-[10px] bg-sky-light object-cover ${ASPECTS[aspect]} ${className}`}
    />
  )
}
