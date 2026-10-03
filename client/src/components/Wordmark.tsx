import { useSyncExternalStore } from 'react'

const LOGO_URL = '/logo.svg'

/**
 * Whether `client/public/logo.svg` exists, probed once per page load and shared
 * by every wordmark on the page. A missing file 404s — or, on a host that
 * rewrites unknown paths to index.html, returns HTML — and either way the image
 * fails to decode, so `onerror` covers both. Dropping the real lettering into
 * `public/` is therefore the whole change; no code needs touching.
 */
let logoStatus: 'checking' | 'present' | 'missing' = 'checking'
const listeners = new Set<() => void>()

function probeLogo() {
  if (typeof Image === 'undefined') return
  const image = new Image()
  image.onload = () => settle('present')
  image.onerror = () => settle('missing')
  image.src = LOGO_URL
}

function settle(status: 'present' | 'missing') {
  logoStatus = status
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  if (listeners.size === 0 && logoStatus === 'checking') probeLogo()
  listeners.add(listener)
  return () => listeners.delete(listener)
}

const LETTERS = 'klowee cafe'.split('')
// k l o w e e · c a f e — every other letter of each word is cobalt.
const COBALT = new Set([1, 3, 6, 8])

type Tone = 'paper' | 'dark'

export function Wordmark({ tone = 'paper', className = '' }: { tone?: Tone; className?: string }) {
  const status = useSyncExternalStore(subscribe, () => logoStatus, () => 'checking' as const)

  if (status === 'present') {
    return <img src={LOGO_URL} alt="Klowee Cafe" className={`inline-block h-[1em] w-auto ${className}`} />
  }

  // Text until (or unless) the SVG proves itself, so there is never a broken
  // image or an empty gap while the probe runs.
  const base = tone === 'paper' ? 'text-navy' : 'text-paper'
  const accent = tone === 'paper' ? 'text-cobalt' : 'text-sky'

  return (
    <span className={`font-heading leading-none whitespace-nowrap ${className}`}>
      <span className="sr-only">Klowee Cafe</span>
      <span aria-hidden="true">
        {LETTERS.map((letter, index) => (
          <span key={index} className={COBALT.has(index) ? accent : base}>
            {letter}
          </span>
        ))}
      </span>
    </span>
  )
}
