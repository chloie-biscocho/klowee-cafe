import { PillButton } from '../components/Button'
import { TentSketch } from '../components/TentSketch'

/** Shown when `/api/public/home` fails. Header and footer still render around it. */
export function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <section
      aria-labelledby="error-heading"
      className="my-8 flex flex-col items-center gap-5 rounded-[28px] bg-panel px-6 py-12 text-center"
    >
      <TentSketch className="w-36" />
      <h1 id="error-heading" className="font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep">
        the cart's offline for a sec, try again?
      </h1>
      <PillButton onClick={onRetry}>try again</PillButton>
    </section>
  )
}
