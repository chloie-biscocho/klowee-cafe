import type { CSSProperties, ReactNode } from 'react'
import { cupLayersFor } from '../lib/cupLayers'
import './scene.css'

const CUPS = ['Spanish Latte', 'Strawberry Matcha', 'Classic Ube Latte']

/**
 * A little coffee cart at the foot of the tab page (handover 17a): striped
 * awning, three cups refilling with the homepage's cup-fill technique, steam
 * rising, and the round logo on the cart as its sign. Decorative only.
 *
 * Built to be extended without new structure: `sign` replaces the logo on the
 * cart's front, and `characters` fills the space behind the counter, where
 * animated Klowe and Kee can stand once layered artwork exists (the logo is a
 * single flat image, so they are not drawn or traced here).
 *
 * A fixed aspect ratio, so it takes its space before it draws: no layout shift.
 */
export default function CartScene({ sign, characters }: { sign?: ReactNode; characters?: ReactNode }) {
  return (
    <div aria-hidden="true" className="pointer-events-none relative mx-auto aspect-[440/200] w-full max-w-[300px] select-none sm:max-w-[400px] lg:max-w-[460px]">
      {/* Behind the counter: the slot for animated characters later. */}
      <div data-slot="characters" className="absolute top-[14%] left-[24%] h-[30%] w-[52%]">
        {characters}
      </div>

      <svg viewBox="0 0 440 200" className="absolute inset-0 h-full w-full" fill="none" strokeLinecap="round" strokeLinejoin="round">
        {/* ground */}
        <path className="stroke-deep" strokeWidth="2.5" d="M30 186c60-3 120 2 190-1s140 3 190-1" opacity="0.5" />
        {/* awning posts */}
        <path className="stroke-deep" strokeWidth="3" d="M112 44v52M328 44v52" />
        {/* awning: stripes, then a scalloped edge */}
        <path className="fill-cloud stroke-deep" strokeWidth="3" d="M92 22h256v22H92z" />
        {[0, 2, 4, 6].map((n) => (
          <path key={n} className="fill-pink" d={`M${92 + n * 32} 23.5h32v19h-32z`} />
        ))}
        <path
          className="fill-cloud stroke-deep"
          strokeWidth="3"
          d="M92 44c0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0 0 9 32 9 32 0"
        />
        {/* counter top and cart body */}
        <path className="fill-paper stroke-deep" strokeWidth="3" d="M96 96h248a6 6 0 0 1 0 12H96a6 6 0 0 1 0-12z" />
        <path className="fill-panel stroke-deep" strokeWidth="3" d="M108 108h224v50a8 8 0 0 1-8 8H116a8 8 0 0 1-8-8z" />
        {/* handle */}
        <path className="stroke-deep" strokeWidth="3" d="M332 124h22l10-14" />
        {/* wheels */}
        <circle className="fill-cloud stroke-deep" strokeWidth="3" cx="150" cy="170" r="16" />
        <circle className="fill-cloud stroke-deep" strokeWidth="3" cx="290" cy="170" r="16" />
        <circle className="fill-deep" cx="150" cy="170" r="3.5" />
        <circle className="fill-deep" cx="290" cy="170" r="3.5" />
      </svg>

      {/* Three cups on the counter, refilling; steam above each. */}
      {CUPS.map((name, i) => {
        const { a, b, c, s1, s2 } = cupLayersFor(name)
        const style = { '--a': a, '--b': b, '--c': c, '--s1': s1, '--s2': s2, '--i': i } as CSSProperties
        const left = 31 + i * 15
        return (
          <div key={name}>
            <div className="scene-steam absolute top-[17%] h-[16%] w-[5%]" style={{ left: `${left + 0.5}%`, '--i': i } as CSSProperties}>
              <svg viewBox="0 0 20 40" className="h-full w-full">
                <path className="stroke-soft" strokeWidth="2.5" fill="none" strokeLinecap="round" d="M10 38c-6-6 6-10 0-17s6-11 0-19" />
              </svg>
            </div>
            <div className="scene-cup absolute top-[30%] h-[18%] w-[8.6%]" style={{ left: `${left}%` }}>
              <div className="cup-glass" style={style} />
              <div className="cup-cover" style={style} />
              <div className="absolute -inset-x-[3px] top-0 h-[11px] rounded-md bg-cloud" />
            </div>
          </div>
        )
      })}

      {/* The cart's sign: the round logo, until characters take its place. */}
      <div data-slot="sign" className="absolute top-[57%] left-1/2 aspect-square w-[16%] -translate-x-1/2 -translate-y-1/2">
        {sign ?? <img src="/logo.jpg" alt="" width={72} height={72} loading="lazy" decoding="async" className="h-full w-full rounded-full border-[3px] border-deep object-cover" />}
      </div>
    </div>
  )
}
