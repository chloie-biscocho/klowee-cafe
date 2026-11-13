import { lazy } from 'react'

/*
 * Every page is its own chunk, fetched the first time its route renders, under
 * the `PageSuspense` boundary in the router. The shell, auth guard and store
 * stay in the initial bundle, so opening the app costs the login page and
 * nothing else. Pages are named exports; `lazy` wants a default one, hence
 * the `.then`.
 */

export const LoginPage = lazy(() =>
  import('../features/auth/LoginPage').then((m) => ({ default: m.LoginPage })),
)
export const VersionsPage = lazy(() =>
  import('../features/menu/VersionsPage').then((m) => ({ default: m.VersionsPage })),
)
export const VersionEditorPage = lazy(() =>
  import('../features/menu/VersionEditorPage').then((m) => ({ default: m.VersionEditorPage })),
)
export const ItemsPage = lazy(() =>
  import('../features/menu/ItemsPage').then((m) => ({ default: m.ItemsPage })),
)
export const CategoriesPage = lazy(() =>
  import('../features/menu/CategoriesPage').then((m) => ({ default: m.CategoriesPage })),
)
export const LoyaltyCardsPage = lazy(() =>
  import('../features/loyalty/LoyaltyCardsPage').then((m) => ({ default: m.LoyaltyCardsPage })),
)
export const AddOnsPage = lazy(() =>
  import('../features/menu/AddOnsPage').then((m) => ({ default: m.AddOnsPage })),
)
export const PackagesPage = lazy(() =>
  import('../features/packages/PackagesPage').then((m) => ({ default: m.PackagesPage })),
)
export const EventsPage = lazy(() =>
  import('../features/events/EventsPage').then((m) => ({ default: m.EventsPage })),
)
export const EventDetailPage = lazy(() =>
  import('../features/events/EventDetailPage').then((m) => ({ default: m.EventDetailPage })),
)
export const AnnouncementsPage = lazy(() =>
  import('../features/announcements/AnnouncementsPage').then((m) => ({
    default: m.AnnouncementsPage,
  })),
)
export const SettingsPage = lazy(() =>
  import('../features/settings/SettingsPage').then((m) => ({ default: m.SettingsPage })),
)
export const DashboardPage = lazy(() =>
  import('../features/sales/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })),
)
export const OrdersPage = lazy(() =>
  import('../features/sales/orders/OrdersPage').then((m) => ({ default: m.OrdersPage })),
)
export const OrderFormPage = lazy(() =>
  import('../features/sales/orders/OrderFormPage').then((m) => ({ default: m.OrderFormPage })),
)
export const OrderDetailPage = lazy(() =>
  import('../features/sales/orders/OrderDetailPage').then((m) => ({ default: m.OrderDetailPage })),
)
export const CustomersPage = lazy(() =>
  import('../features/sales/customers/CustomersPage').then((m) => ({ default: m.CustomersPage })),
)
export const CustomerDetailPage = lazy(() =>
  import('../features/sales/customers/CustomerDetailPage').then((m) => ({ default: m.CustomerDetailPage })),
)
