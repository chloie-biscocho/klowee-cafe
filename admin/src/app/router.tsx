import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { AppShell } from '../components/layout/AppShell'
import { PageSuspense } from '../components/layout/PageSuspense'
import { RequireAuth } from '../features/auth/RequireAuth'
import {
  AddOnsPage,
  LoyaltyCardsPage,
  AnnouncementsPage,
  CategoriesPage,
  CustomerDetailPage,
  CustomersPage,
  DashboardPage,
  OrderDetailPage,
  OrderFormPage,
  OrdersPage,
  EventDetailPage,
  EventsPage,
  ItemsPage,
  LoginPage,
  PackagesPage,
  SettingsPage,
  VersionEditorPage,
  VersionsPage,
} from './pages'

/**
 * Library mode: routes are plain objects, and no route has a `loader`. Data is
 * RTK Query's job, so the router only decides which component renders. Pages
 * load on demand (`./pages`) under a `PageSuspense` boundary.
 * `handle.title` is what the app shell prints above the page.
 *
 * Exported separately from the router so tests can mount the same tree in a
 * memory router.
 */
export const routes: RouteObject[] = [
  { element: <PageSuspense />, children: [{ path: '/login', element: <LoginPage /> }] },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          {
            element: <PageSuspense />,
            children: [
              { index: true, element: <DashboardPage />, handle: { title: 'Dashboard' } },
              { path: '/sales/orders', element: <OrdersPage />, handle: { title: 'Orders' } },
              { path: '/sales/orders/new', element: <OrderFormPage />, handle: { title: 'New order' } },
              { path: '/sales/orders/:id', element: <OrderDetailPage />, handle: { title: 'Order' } },
              { path: '/sales/orders/:id/edit', element: <OrderFormPage />, handle: { title: 'Edit order' } },
              { path: '/sales/customers', element: <CustomersPage />, handle: { title: 'Customers' } },
              { path: '/sales/customers/:id', element: <CustomerDetailPage />, handle: { title: 'Customer' } },
              {
                path: '/menu/versions',
                element: <VersionsPage />,
                handle: { title: 'Menu versions' },
              },
              {
                path: '/menu/versions/:id',
                element: <VersionEditorPage />,
                handle: { title: 'Version editor' },
              },
              { path: '/menu/items', element: <ItemsPage />, handle: { title: 'Menu items' } },
              {
                path: '/menu/categories',
                element: <CategoriesPage />,
                handle: { title: 'Categories' },
              },
              { path: '/menu/add-ons', element: <AddOnsPage />, handle: { title: 'Add-ons' } },
              { path: '/menu/loyalty-cards', element: <LoyaltyCardsPage />, handle: { title: 'Loyalty cards' } },
              { path: '/site/packages', element: <PackagesPage />, handle: { title: 'Packages' } },
              { path: '/site/events', element: <EventsPage />, handle: { title: 'Events' } },
              {
                path: '/site/events/:id',
                element: <EventDetailPage />,
                handle: { title: 'Event' },
              },
              {
                path: '/site/announcements',
                element: <AnnouncementsPage />,
                handle: { title: 'Announcements' },
              },
              {
                path: '/site/settings',
                element: <SettingsPage />,
                handle: { title: 'Site settings' },
              },
              { path: '*', element: <Navigate to="/" replace /> },
            ],
          },
        ],
      },
    ],
  },
]

export const router = createBrowserRouter(routes)
