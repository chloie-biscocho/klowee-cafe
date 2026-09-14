import { NavLink } from 'react-router'
import { useAppDispatch, useAppSelector } from '../../app/hooks'
import { loggedOut, selectUser } from '../../features/auth/authSlice'
import { showToast } from '../../features/toasts/toastSlice'

const MENU_LINKS = [
  { to: '/menu/versions', label: 'Versions' },
  { to: '/menu/items', label: 'Items' },
  { to: '/menu/categories', label: 'Categories' },
  { to: '/menu/add-ons', label: 'Add-ons' },
]

/** Built but not wired up yet, shown so the shape of the app is visible. */
const PLANNED_LINKS = ['Packages', 'Events', 'Announcements', 'Settings']

export function Sidebar() {
  const user = useAppSelector(selectUser)
  const dispatch = useAppDispatch()

  return (
    <aside className="flex w-[220px] shrink-0 flex-col border-r border-line bg-surface">
      <div className="px-5 py-5">
        <span className="text-base font-bold tracking-tight">Klowee</span>
        <span className="ml-1.5 text-xs text-muted">admin</span>
      </div>

      <nav className="flex-1 px-3">
        <p className="px-2 pb-1.5 text-[11px] font-semibold tracking-wide text-muted uppercase">
          Menu
        </p>
        <ul className="flex flex-col gap-0.5">
          {MENU_LINKS.map((link) => (
            <li key={link.to}>
              <NavLink
                to={link.to}
                className={({ isActive }) =>
                  `block rounded-md px-2 py-1.5 text-sm ${
                    isActive
                      ? 'bg-accent-soft font-semibold text-accent'
                      : 'text-ink hover:bg-canvas'
                  }`
                }
              >
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>

        <p className="px-2 pt-5 pb-1.5 text-[11px] font-semibold tracking-wide text-muted uppercase">
          Later
        </p>
        <ul className="flex flex-col gap-0.5">
          {PLANNED_LINKS.map((label) => (
            <li
              key={label}
              aria-disabled="true"
              className="flex items-center justify-between rounded-md px-2 py-1.5 text-sm text-muted/60"
            >
              {label}
              <span className="text-[10px] tracking-wide uppercase">soon</span>
            </li>
          ))}
        </ul>
      </nav>

      <div className="border-t border-line px-5 py-4">
        <p className="truncate text-sm font-medium">{user?.displayName ?? 'Signed in'}</p>
        <button
          type="button"
          className="mt-1 text-xs text-muted underline-offset-2 hover:text-ink hover:underline"
          onClick={() => {
            dispatch(loggedOut())
            dispatch(showToast('success', 'Signed out.'))
          }}
        >
          Log out
        </button>
      </div>
    </aside>
  )
}
