import { Pill } from '../components/Pill'
import { Wordmark } from '../components/Wordmark'

const LINKS = [
  { href: '#story', label: 'story' },
  { href: '#menu', label: 'menu' },
  { href: '#packages', label: 'packages' },
  { href: '#popups', label: 'pop-ups' },
  { href: '#book', label: 'book' },
]

/** Sticky paper bar. Narrow screens keep only the wordmark and the call to action. */
export function Nav({ hasPackages = true }: { hasPackages?: boolean }) {
  // The packages section disappears when there are none; so does its link.
  const links = hasPackages ? LINKS : LINKS.filter((link) => link.href !== '#packages')

  return (
    <header className="sticky top-0 z-30 border-b-2 border-cobalt bg-paper">
      <nav
        aria-label="Main"
        className="mx-auto flex max-w-page items-center justify-between gap-4 px-5 py-3 md:px-8"
      >
        <a href="#top" className="text-3xl">
          <Wordmark />
        </a>
        <ul className="hidden items-center gap-6 md:flex">
          {links.map((link) => (
            <li key={link.href}>
              <a href={link.href} className="font-medium text-navy hover:text-cobalt">
                {link.label}
              </a>
            </li>
          ))}
        </ul>
        <Pill href="#book">Book the cart</Pill>
      </nav>
    </header>
  )
}
