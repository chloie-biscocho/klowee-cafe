import { useEffect } from 'react'
import { ThemeSwitch } from '../components/ThemeSwitch'
import { ErrorState } from '../sections/ErrorState'
import { tokenFromPath } from './format'
import { LoyaltyCards } from './LoyaltyCards'
import { Receipt } from './Receipt'
import { RecentPayments } from './RecentPayments'
import { useTab } from './useTab'
import './tab.css'

function PageHeader({ name }: { name?: string }) {
  return (
    <header className="flex items-center gap-2.5 px-1 py-0.5 lg:col-span-2">
      <img src="/logo.jpg" alt="" width={42} height={42} className="size-[42px] flex-none rounded-full" />
      <div className="min-w-0 flex-1">
        <p className="font-serif text-[1.05rem] leading-[1.1] text-deep">klowee cafe</p>
        {name && <p className="text-[0.92rem] text-soft">hi, {name}</p>}
      </div>
      <ThemeSwitch />
    </header>
  )
}

/**
 * A customer's private page (handover 17, decision 021), at /tab/{token}:
 * the receipt and recent payments on the left, the loyalty cards on the
 * right at computer width; one column on a phone. Not indexed, no analytics,
 * nothing stored but the theme choice.
 */
export default function TabPage({ path }: { path: string }) {
  const tab = useTab(tokenFromPath(path))

  useEffect(() => {
    document.title = 'your tab · klowee cafe'
    // The head script in index.html adds this before first paint; this covers
    // a page reached any other way. The API and Vercel also send X-Robots-Tag.
    if (!document.querySelector('meta[name="robots"]')) {
      const meta = document.createElement('meta')
      meta.name = 'robots'
      meta.content = 'noindex'
      document.head.append(meta)
    }
  }, [])

  return (
    <div className="mx-auto flex max-w-[480px] flex-col gap-3.5 px-4 pt-[18px] pb-[26px] lg:grid lg:max-w-page lg:grid-cols-[minmax(0,1fr)_minmax(0,1.08fr)] lg:items-start lg:gap-4 lg:px-6 lg:pt-[22px]">
      <PageHeader name={tab.status === 'ready' ? tab.data.name : undefined} />

      {tab.status === 'loading' && (
        <div aria-busy="true" aria-label="Loading your tab" className="flex flex-col gap-3.5 lg:col-span-2 lg:grid lg:grid-cols-[minmax(0,1fr)_minmax(0,1.08fr)] lg:gap-4">
          <div className="h-72 rounded-md bg-paper/60" />
          <div className="h-96 rounded-[22px] bg-panel/60" />
        </div>
      )}

      {tab.status === 'error' && (
        <div className="lg:col-span-2">
          <ErrorState onRetry={tab.retry} />
        </div>
      )}

      {tab.status === 'gone' && (
        <section aria-labelledby="gone-heading" className="rounded-[22px] bg-panel px-6 py-10 text-center lg:col-span-2">
          <h1 id="gone-heading" className="font-serif text-[clamp(1.5rem,4vw,2rem)] leading-[1.15] text-deep">
            this link isn't active anymore
          </h1>
          <p className="mt-3 text-soft">Ask Klowee for a new one, and we'll send it right over.</p>
        </section>
      )}

      {tab.status === 'ready' && (
        <>
          <div className="lg:col-start-1 lg:row-start-2">
            <Receipt tab={tab.data} onClaim={tab.submitClaim} />
          </div>
          <div className="lg:col-start-1 lg:row-start-3">
            <RecentPayments payments={tab.data.recentPayments} />
          </div>
          <div className="lg:col-start-2 lg:row-span-2 lg:row-start-2">
            <LoyaltyCards clubs={tab.data.clubs} onRequest={tab.requestCard} />
          </div>
          <p className="px-2.5 text-center text-[0.85rem] text-soft lg:col-span-2">This page is only for you. Don't share the link.</p>
        </>
      )}
    </div>
  )
}
