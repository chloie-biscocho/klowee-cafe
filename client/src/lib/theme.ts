/**
 * Light by default, dark on request (decision 019). The site ignores the
 * device's colour-scheme setting: a visitor sees light until they press the
 * switch, and the choice is remembered in localStorage.
 *
 * index.html repeats THEME_KEY and the dark THEME_COLOR in a small head script
 * that applies a stored dark choice before first paint; ThemeSwitch.test.tsx
 * runs that script, so the two copies cannot drift apart.
 */
export type Theme = 'light' | 'dark'

export const THEME_KEY = 'klowee.theme'

/** The browser chrome colour for each theme: the page's sky. */
export const THEME_COLOR: Record<Theme, string> = { light: '#A9D6F5', dark: '#1A2452' }

/** The theme on the page now: dark only when <html data-theme="dark">. */
export function currentTheme(): Theme {
  if (typeof document === 'undefined') return 'light'
  return document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light'
}

/** Shows `theme` and remembers it. Storage can be blocked; the switch still works for this page. */
export function setTheme(theme: Theme) {
  document.documentElement.dataset.theme = theme
  document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')?.setAttribute('content', THEME_COLOR[theme])
  try {
    localStorage.setItem(THEME_KEY, theme)
  } catch {
    // Private mode or blocked site data: nothing to remember it in.
  }
}
