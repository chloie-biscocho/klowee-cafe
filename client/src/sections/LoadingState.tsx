/**
 * The page's shape while `/api/public/home` is in flight: a paper hero card on
 * the sky band and three blocks where sections will be. No spinner, nothing
 * moves; it just holds the layout so the page does not jump when data lands.
 */
export function LoadingState() {
  return (
    <div aria-busy="true" aria-label="Loading">
      <div className="bg-sky pt-14 pb-16 md:pt-20 md:pb-24">
        <div className="mx-auto max-w-page px-5 md:px-8">
          <div className="mx-auto flex max-w-2xl flex-col items-center gap-4 rounded-card border-b-2 border-cobalt bg-paper px-6 py-10">
            <div className="h-10 w-48 rounded-full bg-sky-light" />
            <div className="h-12 w-full max-w-md rounded-full bg-sky-light" />
            <div className="h-5 w-full max-w-sm rounded-full bg-sky-light" />
            <div className="mt-4 flex gap-3">
              <div className="h-11 w-36 rounded-full bg-sky-light" />
              <div className="h-11 w-36 rounded-full bg-sky-light" />
            </div>
          </div>
          <div className="mt-10 aspect-[16/7] rounded-[10px] bg-sky-light md:mt-14" />
        </div>
      </div>
      {[0, 1, 2].map((block) => (
        <div key={block} className="mx-auto max-w-page px-5 py-12 md:px-8">
          <div className="h-56 rounded-card bg-sky-light" />
        </div>
      ))}
    </div>
  )
}
