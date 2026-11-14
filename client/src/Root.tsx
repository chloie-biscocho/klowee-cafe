import { lazy, Suspense } from 'react'
import { App } from './App'

// No router: one other page, picked by path (handover 17). A customer's tab
// page loads its own chunk, so the homepage never downloads it, and the tab
// page never renders the homepage. Vercel's rewrite serves index.html for both.
const TabPage = lazy(() => import('./tab/TabPage'))

export function Root({ path }: { path: string }) {
  return path.startsWith('/tab/') ? (
    <Suspense fallback={null}>
      <TabPage path={path} />
    </Suspense>
  ) : (
    <App />
  )
}
