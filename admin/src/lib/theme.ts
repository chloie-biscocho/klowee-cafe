/**
 * Light by default, dark on request (handover 18, decision 022), as on the
 * public site but stored under its own key, so the two apps keep separate
 * choices. index.html repeats THEME_KEY in a small head script that applies a
 * stored dark choice before first paint; theme.test.ts runs that script, so
 * the two copies cannot drift apart.
 */
export type Theme = 'light' | 'dark'

export const THEME_KEY = 'klowee.admin.theme'

/** The theme on the page now: dark only when <html data-theme="dark">. */
export function currentTheme(): Theme {
  if (typeof document === 'undefined') return 'light'
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light'
}

/** Shows `theme` and remembers it. Storage can be blocked; the switch still works for this page. */
export function setTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  try {
    localStorage.setItem(THEME_KEY, theme)
  } catch {
    // Private mode or blocked site data: nothing to remember it in.
  }
}
