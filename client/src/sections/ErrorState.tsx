import { PaperCard } from '../components/PaperCard'
import { PillButton } from '../components/Pill'
import { TentSketch } from '../components/TentSketch'

/** Shown when `/api/public/home` fails. Nav and footer still render around it. */
export function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <section aria-labelledby="error-heading" className="bg-sky py-16 md:py-24">
      <div className="mx-auto max-w-page px-5 md:px-8">
        <PaperCard tape className="mx-auto flex max-w-xl flex-col items-center gap-5 px-6 py-12 text-center">
          <TentSketch className="w-36" />
          <h1 id="error-heading" className="font-heading text-3xl leading-[1.05] text-navy md:text-4xl">
            the cart's offline for a sec, try again?
          </h1>
          <PillButton onClick={onRetry}>try again</PillButton>
        </PaperCard>
      </div>
    </section>
  )
}
