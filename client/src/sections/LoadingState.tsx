/**
 * The page's shape while `/api/public/home` is in flight: the hero card and
 * its photo on the sky band, then blocks where sections will be. No spinner,
 * nothing moves. The card uses the hero's own spacing (wordmark, two heading
 * lines, body, buttons) and the photo its 16:7 slot, so the page does not jump
 * when the data lands. The sticker strip above the nav is held by StickerBar.
 */
export function LoadingState() {
  return (
    <div aria-busy="true" aria-label="Loading">
      <div className="bg-sky pt-14 pb-16 md:pt-20 md:pb-24">
        <div className="mx-auto max-w-page px-5 md:px-8">
          <div className="mx-auto flex max-w-2xl flex-col items-center rounded-card border-b-2 border-cobalt bg-paper px-6 pt-10 pb-8 md:px-12">
            {/* Wordmark: text-4xl / md:text-5xl, one line. */}
            <div className="h-10 w-48 rounded-full bg-sky-light md:h-12" />
            {/* Heading: mt-6, two lines of text-4xl / md:text-6xl at 1.05. */}
            <div className="mt-6 h-[4.725rem] w-full max-w-md rounded-[18px] bg-sky-light md:h-[7.875rem]" />
            {/* Body: mt-5, one paragraph of text-lg. */}
            <div className="mt-5 h-14 w-full max-w-sm rounded-[14px] bg-sky-light" />
            {/* The two pills: mt-8, 44px tall. */}
            <div className="mt-8 flex gap-3">
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
