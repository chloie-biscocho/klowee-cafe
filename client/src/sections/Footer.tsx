import type { PublicHome } from '../api/types'
import { Wordmark } from '../components/Wordmark'
import { setting } from '../lib/content'

/** Navy band, paper text. Renders without data too (loading and error states). */
export function Footer({ home }: { home: PublicHome | null }) {
  const links = [
    { label: 'Instagram', href: setting(home, 'instagram_url') },
    { label: 'Facebook', href: setting(home, 'facebook_url') },
  ].filter((link) => link.href)
  const email = setting(home, 'contact_email')

  return (
    <footer className="bg-navy py-10 text-paper">
      <div className="mx-auto flex max-w-page flex-col gap-4 px-5 md:flex-row md:items-center md:justify-between md:px-8">
        <div>
          <p className="text-2xl">
            <Wordmark tone="dark" />
          </p>
          <p className="mt-2 text-sm">© {new Date().getFullYear()} Klowee Cafe · Cagayan de Oro</p>
        </div>
        {(links.length > 0 || email) && (
          <ul className="flex flex-wrap gap-x-6 gap-y-2">
            {links.map((link) => (
              <li key={link.label}>
                {/* Cobalt on navy is invisible, so the ring is paper here. */}
                <a
                  href={link.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="underline-offset-4 hover:text-sky hover:underline focus-visible:outline-paper"
                >
                  {link.label}
                </a>
              </li>
            ))}
            {email && (
              <li>
                <a
                  href={`mailto:${email}`}
                  className="underline-offset-4 hover:text-sky hover:underline focus-visible:outline-paper"
                >
                  {email}
                </a>
              </li>
            )}
          </ul>
        )}
      </div>
    </footer>
  )
}
