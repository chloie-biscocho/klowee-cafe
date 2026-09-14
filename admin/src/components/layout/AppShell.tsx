import { Outlet, useMatches } from 'react-router'
import { useGetMeQuery } from '../../api/authApi'
import { Sidebar } from './Sidebar'

/** Routes declare their page title through `handle`. */
interface RouteHandle {
  title?: string
}

function usePageTitle(): string {
  const matches = useMatches()
  for (let index = matches.length - 1; index >= 0; index -= 1) {
    const handle = matches[index]?.handle as RouteHandle | undefined
    if (handle?.title) return handle.title
  }
  return 'Klowee admin'
}

export function AppShell() {
  // Re-checks the stored token against the API on load. If it expired while the
  // tab was closed, the 401 handler in baseApi signs the owner out from here.
  useGetMeQuery()

  const title = usePageTitle()

  return (
    <div className="flex min-h-screen">
      <Sidebar />
      <main className="flex-1 overflow-x-hidden">
        <div className="mx-auto max-w-[1100px] px-8 py-7">
          <h1 className="mb-5 text-xl font-bold tracking-tight">{title}</h1>
          <Outlet />
        </div>
      </main>
    </div>
  )
}
