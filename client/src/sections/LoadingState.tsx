/**
 * Below the hero while `/api/public/home` is in flight: quiet blocks where the
 * cups, the story and the menu will be. The hero itself renders its own shell
 * (Hero with no data), which is what holds the first screen still.
 */
export function LoadingState() {
  return (
    <div aria-busy="true" aria-label="Loading">
      <div className="mx-auto h-[170px] max-w-[760px] md:h-[200px]" />
      <div className="mb-[26px] h-64 rounded-[28px] bg-panel/60" />
      <div className="mb-[26px] h-96 rounded-md bg-paper/60" />
    </div>
  )
}
