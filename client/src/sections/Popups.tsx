import { Fragment, useEffect, useRef, useState } from 'react'
import type { PublicHome } from '../api/types'
import { EventPanel } from '../components/EventPanel'
import { PaperCard } from '../components/PaperCard'
import { Pill } from '../components/Pill'
import { Polaroid } from '../components/Polaroid'
import { Section } from '../components/Section'
import { TentSketch } from '../components/TentSketch'
import { nextFirst } from '../lib/content'

export function Popups({ home }: { home: PublicHome }) {
  const nextId = home.nextEvent?.status === 'Upcoming' ? home.nextEvent.id : null
  const events = nextFirst(home.events, nextId)
  const hasUpcoming = events.some((event) => event.id === nextId)

  const [expandedId, setExpandedId] = useState<string | null>(null)
  const buttons = useRef(new Map<string, HTMLButtonElement>())

  const close = () => {
    if (!expandedId) return
    // Hand focus back to the polaroid that opened the panel.
    buttons.current.get(expandedId)?.focus()
    setExpandedId(null)
  }

  // Escape closes the open panel from anywhere on the page. A UI listener, not data.
  useEffect(() => {
    if (!expandedId) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      buttons.current.get(expandedId)?.focus()
      setExpandedId(null)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [expandedId])

  return (
    <Section
      id="popups"
      band="paper"
      heading={hasUpcoming ? "where we're headed" : "where we've been"}
    >
      {events.length === 0 ? (
        <PaperCard className="flex flex-col items-center gap-4 px-6 py-12 text-center">
          <TentSketch className="w-40" />
          <p className="font-hand text-3xl font-semibold text-navy">no pop-ups logged yet</p>
          <Pill href="#book">Book the cart</Pill>
        </PaperCard>
      ) : (
        // `grid-flow-row-dense`: the panel spans a whole row, and the dense
        // flow pulls the rest of the clicked row up past it, so the panel
        // always lands directly under that row at 1, 2 or 3 columns.
        <ul className="grid grid-flow-row-dense grid-cols-1 gap-x-8 gap-y-10 px-2 sm:grid-cols-2 md:grid-cols-3">
          {events.map((event, index) => {
            const panelId = `popup-panel-${event.id}`
            const expanded = expandedId === event.id
            return (
              <Fragment key={event.id}>
                <li>
                  <Polaroid
                    event={event}
                    index={index}
                    isNext={event.id === nextId}
                    expanded={expanded}
                    panelId={panelId}
                    onToggle={() => setExpandedId(expanded ? null : event.id)}
                    buttonRef={(element) => {
                      if (element) buttons.current.set(event.id, element)
                      else buttons.current.delete(event.id)
                    }}
                  />
                </li>
                {expanded && (
                  <li className="col-span-full">
                    <EventPanel event={event} id={panelId} onClose={close} />
                  </li>
                )}
              </Fragment>
            )
          })}
        </ul>
      )}
    </Section>
  )
}
