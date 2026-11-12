import type { PublicHome } from '../api/types'
import { setting } from '../lib/content'

/** The copyright line and the two social links. Renders without data too (loading and error). */
export function Footer({ home }: { home: PublicHome | null }) {
  const links = [
    { label: 'Instagram', href: setting(home, 'instagram_url') },
    { label: 'Facebook', href: setting(home, 'facebook_url') },
  ].filter((link) => link.href)

  return (
    <footer className="flex flex-wrap justify-between gap-3 pt-4 pb-11 text-soft">
      <p>© {new Date().getFullYear()} Klowee Cafe, Cagayan de Oro</p>
      {links.length > 0 && (
        <ul className="flex gap-4">
          {links.map((link) => (
            <li key={link.label}>
              <a href={link.href} target="_blank" rel="noopener noreferrer" className="text-deep underline">
                {link.label}
              </a>
            </li>
          ))}
        </ul>
      )}
    </footer>
  )
}
