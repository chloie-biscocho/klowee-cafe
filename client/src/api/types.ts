/**
 * The shape of `GET /api/public/home`, mirroring
 * `api/Klowee.Api/Contracts/Public/PublicHomeContracts.cs`.
 * `DateOnly` arrives as "2026-05-21"; `decimal` as a JSON number; enums as strings.
 */

export type EventStatus = 'Planned' | 'Upcoming' | 'Done' | 'Cancelled'

export interface PublicAnnouncement {
  text: string
  linkUrl: string | null
}

export interface PublicEventSummary {
  id: string
  name: string
  venue: string
  startsOn: string
  endsOn: string
  coverPhotoUrl: string | null
}

export interface PublicMenuItem {
  id: string
  name: string
  description: string
  price: number
  photoUrl: string | null
  isFeatured: boolean
}

export interface PublicMenuCategory {
  id: string
  name: string
  items: PublicMenuItem[]
}

export interface PublicMenu {
  versionName: string
  effectiveFrom: string
  categories: PublicMenuCategory[]
}

export interface PublicPackage {
  id: string
  name: string
  price: number
  description: string
  guestCountNote: string
  inclusions: string[]
}

export interface PublicEventPhoto {
  url: string
  caption: string | null
}

export interface PublicEvent {
  id: string
  name: string
  venue: string
  startsOn: string
  endsOn: string
  status: EventStatus
  coverPhotoUrl: string | null
  description: string | null
  photos: PublicEventPhoto[]
}

export interface PublicHome {
  /** Only keys that have been saved appear; read them through `setting()`. */
  settings: Record<string, string>
  announcement: PublicAnnouncement | null
  nextEvent: PublicEventSummary | null
  menu: PublicMenu | null
  packages: PublicPackage[]
  events: PublicEvent[]
}
