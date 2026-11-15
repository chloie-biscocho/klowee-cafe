import { stampTilt } from '../../../lib/loyalty'

/**
 * The rubber stamp pressed over a filled slot (handover 18, part 3; the
 * customer page's stamp, ported): SIPPED, or CLAIMED on a reward that was
 * used. Solid ink in a thick outline on a light wash, so the word measures
 * against the wash on any slot: cobalt on cream, cream on the club-colour slot.
 * Tilted by `stampTilt`, the same formula as the client, so both apps press
 * the same card. Decorative: the slot carries the words for screen readers.
 */
export function StampMark({ slot, claimed, onClubColour, matcha }: { slot: number; claimed: boolean; onClubColour: boolean; matcha: boolean }) {
  return (
    <span
      aria-hidden="true"
      className={`absolute top-1/2 left-1/2 z-[1] rounded-[4px] border-[2.5px] px-[0.28em] pt-[0.12em] pb-[0.06em] font-display text-[clamp(0.56rem,0.9vw,0.82rem)] leading-none whitespace-nowrap shadow-[0_0_0_1px_var(--color-card)] ${
        onClubColour ? `border-card text-card ${matcha ? 'bg-matcha/85' : 'bg-cobalt/85'}` : 'border-cobalt bg-card/80 text-cobalt'
      }`}
      style={{ transform: `translate(-50%, -50%) rotate(${stampTilt(slot)}deg)` }}
    >
      {claimed ? 'CLAIMED' : 'SIPPED'}
    </span>
  )
}
