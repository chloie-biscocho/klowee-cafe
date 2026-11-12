const LINKS = [
  { href: '#story', label: 'our story' },
  { href: '#menu', label: 'menu' },
  { href: '#popups', label: 'pop-ups' },
  { href: '#packages', label: 'packages' },
  { href: '#book', label: 'book' },
]

/** Text wordmark and the section links. Not sticky; no logo up here (the logo lives in the hero). */
export function Header({ hasPackages = true }: { hasPackages?: boolean }) {
  // No packages, no packages section, and so no link to it.
  const links = hasPackages ? LINKS : LINKS.filter((link) => link.href !== '#packages')

  return (
    <header className="flex flex-wrap items-center justify-between gap-3.5 py-[18px]">
      <a href="#top" aria-label="Klowee Cafe home" className="font-serif text-[1.2rem] text-deep no-underline">
        klowee cafe
      </a>
      <nav aria-label="Main">
        <ul className="flex flex-wrap">
          {links.map((link) => (
            <li key={link.href}>
              <a
                href={link.href}
                className="mr-4 inline-block font-medium whitespace-nowrap text-deep no-underline underline-offset-[5px] hover:underline md:mr-0 md:ml-5"
              >
                {link.label}
              </a>
            </li>
          ))}
        </ul>
      </nav>
    </header>
  )
}
