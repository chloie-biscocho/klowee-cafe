import { useState } from 'react'
import { NavLink } from 'react-router'
import { useGetSummaryQuery } from '../../api/salesApi'
import { useAppDispatch, useAppSelector } from '../../app/hooks'
import { ChangePasswordModal } from '../../features/auth/ChangePasswordModal'
import { loggedOut, selectUser } from '../../features/auth/authSlice'
import { showToast } from '../../features/toasts/toastSlice'
import { monthRange, todayInManila } from '../../lib/dates'
import { ThemeSwitch } from './ThemeSwitch'

interface NavGroup {
  heading: string
  links: { to: string; label: string }[]
}

const NAV_GROUPS: NavGroup[] = [
  {
    heading: 'Sales',
    links: [
      { to: '/', label: 'Dashboard' },
      { to: '/sales/orders', label: 'Orders' },
      { to: '/sales/customers', label: 'Customers' },
    ],
  },
  {
    heading: 'Menu',
    links: [
      { to: '/menu/versions', label: 'Versions' },
      { to: '/menu/items', label: 'Items' },
      { to: '/menu/categories', label: 'Categories' },
      { to: '/menu/add-ons', label: 'Add-ons' },
      { to: '/menu/loyalty-cards', label: 'Loyalty cards' },
    ],
  },
  {
    heading: 'Site',
    links: [
      { to: '/site/packages', label: 'Packages' },
      { to: '/site/events', label: 'Events' },
      { to: '/site/bookings', label: 'Bookings' },
      { to: '/site/announcements', label: 'Announcements' },
      { to: '/site/settings', label: 'Settings' },
    ],
  },
]

export function Sidebar() {
  const user = useAppSelector(selectUser)
  const dispatch = useAppDispatch()
  const [changingPassword, setChangingPassword] = useState(false)
  // The dashboard's own summary (same month, same cache entry): what's waiting
  // for the owners, payments to confirm and cards asked for (handover 17).
  const [from, to] = monthRange(todayInManila())
  const { data: summary } = useGetSummaryQuery({ from, to })
  // Booking requests still New count too (handover 20).
  const waiting = (summary?.pendingClaimsCount ?? 0) + (summary?.pendingCardRequestsCount ?? 0) + (summary?.newBookingsCount ?? 0)

  return (
    <aside className="relative flex shrink-0 flex-col overflow-hidden bg-side text-side-ink wide:w-[232px] max-wide:flex-row max-wide:flex-wrap max-wide:items-center max-wide:gap-x-1 max-wide:gap-y-1 max-wide:px-3 max-wide:py-2.5">
      {/* Two soft clouds low in the sidebar, light mode only (transparent in dark). */}
      <div aria-hidden="true" className="pointer-events-none absolute bottom-[110px] -left-[26px] h-[38px] w-[130px] rounded-full bg-side-cloud before:absolute before:-top-6 before:left-[26px] before:size-[52px] before:rounded-full before:bg-side-cloud max-wide:hidden" />
      <div aria-hidden="true" className="pointer-events-none absolute -right-10 bottom-[190px] h-[38px] w-[130px] scale-[0.7] rounded-full bg-side-cloud opacity-80 before:absolute before:-top-6 before:left-[26px] before:size-[52px] before:rounded-full before:bg-side-cloud max-wide:hidden" />

      <div className="relative flex items-center gap-2.5 px-5 pt-5 pb-3 max-wide:p-0 max-wide:pr-2">
        <img src="/logo.jpg" alt="" width={46} height={46} className="size-[46px] flex-none rounded-full max-wide:size-9" />
        <div>
          <span className="block font-serif text-[1.12rem] leading-[1.1]">klowee cafe</span>
          <span className="text-[0.82rem] text-side-soft">admin</span>
        </div>
      </div>

      <nav aria-label="Main" className="relative flex-1 px-3 max-wide:order-3 max-wide:flex max-wide:basis-full max-wide:flex-wrap max-wide:px-0">
        {NAV_GROUPS.map((group) => (
          <div key={group.heading} className="max-wide:contents">
            <p className="px-3 pt-4 pb-1 text-[0.78rem] font-bold text-side-soft max-wide:hidden">{group.heading}</p>
            <ul className="flex flex-col gap-0.5 max-wide:flex-row max-wide:flex-wrap">
              {group.links.map((link) => (
                <li key={link.to}>
                  <NavLink
                    to={link.to}
                    // "/" would otherwise match, and highlight, every page.
                    end={link.to === '/'}
                    className={({ isActive }) =>
                      `block rounded-full px-3 py-1.5 text-[0.95rem] ${
                        isActive ? 'bg-nav-active font-bold text-side-ink' : 'font-medium text-side-ink hover:bg-nav-active/50'
                      }`
                    }
                  >
                    <span className="flex items-center justify-between gap-2">
                      {link.label}
                      {link.to === '/' && waiting > 0 && (
                        <span
                          className="min-w-5 rounded-full bg-accent px-1.5 text-center text-[11px] leading-5 font-semibold text-on-accent"
                          aria-label={`${waiting} waiting`}
                        >
                          {waiting}
                        </span>
                      )}
                    </span>
                  </NavLink>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <div className="relative px-5 pt-3 pb-4 max-wide:order-2 max-wide:ml-auto max-wide:flex max-wide:items-center max-wide:gap-3 max-wide:p-0">
        <ThemeSwitch className="mb-3 max-wide:mb-0" />
        <p className="truncate text-sm font-bold max-wide:hidden">{user?.displayName ?? 'Signed in'}</p>
        <div className="mt-1 flex gap-3 max-wide:mt-0">
          <button
            type="button"
            className="cursor-pointer text-xs text-side-soft underline-offset-2 hover:text-side-ink hover:underline"
            onClick={() => setChangingPassword(true)}
          >
            Change password
          </button>
          <button
            type="button"
            className="cursor-pointer text-xs text-side-soft underline-offset-2 hover:text-side-ink hover:underline"
            onClick={() => {
              dispatch(loggedOut())
              dispatch(showToast('success', 'Signed out.'))
            }}
          >
            Log out
          </button>
        </div>
      </div>
      {changingPassword && <ChangePasswordModal onClose={() => setChangingPassword(false)} />}
    </aside>
  )
}
