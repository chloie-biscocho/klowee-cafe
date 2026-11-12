import { Fragment, useEffect, useRef, useState } from 'react'
import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { EventPanel } from '../components/EventPanel'
import { Polaroid } from '../components/Polaroid'
import { TentSketch } from '../components/TentSketch'
import { nextFirst, polaroidBadge } from '../lib/content'

/** Polaroids of every published pop-up; one opens a panel under its own row. */
export function Popups({ home }: { home: PublicHome }) {
  const nextId = home.nextEvent?.status === 'Upcoming' ? home.nextEvent.id : null
  const events = nextFirst(home.events, nextId)
  const hasUpcoming = nextId !== null && events.some((event) => event.id === nextId)
  const badge = polaroidBadge(events, nextId)

  const [openId, setOpenId] = useState<string | null>(null)
  const buttons = useRef(new Map<string, HTMLButtonElement>())

  const close = () => {
    if (!openId) return
    buttons.current.get(openId)?.focus() // back to the polaroid that opened it
    setOpenId(null)
  }

  // Escape closes the open panel from anywhere on the page. A UI listener, not data.
  useEffect(() => {
    if (!openId) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      buttons.current.get(openId)?.focus()
      setOpenId(null)
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [openId])

  return (
    <section id="popups" aria-labelledby="popups-heading" className="pt-5 pb-[30px]">
      <h2 id="popups-heading" className="mb-1.5 font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep">
        {hasUpcoming ? "Where we're headed" : "Where we've been"}
      </h2>
      {events.length === 0 ? (
        <div className="mt-4 flex flex-col items-center gap-4 rounded-[22px] bg-panel px-6 py-12 text-center">
          <TentSketch className="w-36" />
          <p className="font-hand text-[1.3rem] font-medium text-deep">no pop-ups logged yet</p>
          <LinkButton href="#book">Book the cart</LinkButton>
        </div>
      ) : (
        <>
          <p className="mb-[22px] max-w-[56ch] text-soft">Tap a photo to see more from that pop-up.</p>
          {/* Dense flow: the panel spans a row, and the rest of the clicked row is pulled up past it,
              so the panel lands directly under that row at 1, 2 or 3 columns. */}
          <ul className="grid grid-flow-row-dense grid-cols-1 gap-x-6 gap-y-[30px] pt-2.5 sm:grid-cols-2 lg:grid-cols-3">
            {events.map((event, index) => {
              const panelId = `popup-panel-${event.id}`
              const open = openId === event.id
              return (
                <Fragment key={event.id}>
                  <li>
                    <Polaroid
                      event={event}
                      index={index}
                      badge={badge?.id === event.id ? badge.label : null}
                      expanded={open}
                      panelId={panelId}
                      onToggle={() => setOpenId(open ? null : event.id)}
                      buttonRef={(element) => {
                        if (element) buttons.current.set(event.id, element)
                        else buttons.current.delete(event.id)
                      }}
                    />
                  </li>
                  {open && (
                    <li className="col-span-full">
                      <EventPanel event={event} id={panelId} onClose={close} />
                    </li>
                  )}
                </Fragment>
              )
            })}
          </ul>
        </>
      )}
    </section>
  )
}
