import { Suspense } from 'react'
import { Outlet } from 'react-router'
import { Spinner } from '../ui/Spinner'

/**
 * The boundary for lazily loaded pages (`app/pages.tsx`). It sits inside the
 * shell, so while a page's chunk is in flight the sidebar stays put and only
 * the content area shows the canvas and a small spinner.
 */
export function PageSuspense() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-40 items-center justify-center bg-canvas text-muted">
          <Spinner className="size-5" />
        </div>
      }
    >
      <Outlet />
    </Suspense>
  )
}
