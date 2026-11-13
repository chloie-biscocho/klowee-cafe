import { useState } from 'react'
import { currentTheme, setTheme, type Theme } from '../lib/theme'

/**
 * The header's light/dark switch. Its label names the theme it switches to;
 * the glyph is decoration, so the accessible name is just "Dark mode" or
 * "Light mode". It starts from <html data-theme>, which the head script in
 * index.html has already set from a stored choice.
 */
export function ThemeSwitch({ className = '' }: { className?: string }) {
  const [theme, setState] = useState<Theme>(currentTheme)
  const next: Theme = theme === 'dark' ? 'light' : 'dark'

  return (
    <button
      type="button"
      onClick={() => {
        setTheme(next)
        setState(next)
      }}
      className={`cursor-pointer rounded-full border-0 bg-panel px-3.5 py-[7px] text-[0.9rem] font-bold whitespace-nowrap text-deep ${className}`}
    >
      <span aria-hidden="true">{next === 'dark' ? '☾' : '☀'}</span> {next === 'dark' ? 'Dark mode' : 'Light mode'}
    </button>
  )
}
