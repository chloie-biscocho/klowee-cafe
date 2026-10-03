import { createBrowserRouter, Navigate, type RouteObject } from 'react-router'
import { AppShell } from '../components/layout/AppShell'
import { LoginPage } from '../features/auth/LoginPage'
import { RequireAuth } from '../features/auth/RequireAuth'
import { AnnouncementsPage } from '../features/announcements/AnnouncementsPage'
import { EventDetailPage } from '../features/events/EventDetailPage'
import { EventsPage } from '../features/events/EventsPage'
import { AddOnsPage } from '../features/menu/AddOnsPage'
import { CategoriesPage } from '../features/menu/CategoriesPage'
import { ItemsPage } from '../features/menu/ItemsPage'
import { VersionEditorPage } from '../features/menu/VersionEditorPage'
import { VersionsPage } from '../features/menu/VersionsPage'
import { PackagesPage } from '../features/packages/PackagesPage'
import { SettingsPage } from '../features/settings/SettingsPage'

/**
 * Library mode: routes are plain objects, and no route has a `loader`. Data is
 * RTK Query's job, so the router only decides which component renders.
 * `handle.title` is what the app shell prints above the page.
 *
 * Exported separately from the router so tests can mount the same tree in a
 * memory router.
 */
export const routes: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/menu/versions" replace /> },
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
          { path: '/site/packages', element: <PackagesPage />, handle: { title: 'Packages' } },
          { path: '/site/events', element: <EventsPage />, handle: { title: 'Events' } },
          { path: '/site/events/:id', element: <EventDetailPage />, handle: { title: 'Event' } },
          {
            path: '/site/announcements',
            element: <AnnouncementsPage />,
            handle: { title: 'Announcements' },
          },
          { path: '/site/settings', element: <SettingsPage />, handle: { title: 'Site settings' } },
          { path: '*', element: <Navigate to="/menu/versions" replace /> },
        ],
      },
    ],
  },
]

export const router = createBrowserRouter(routes)
