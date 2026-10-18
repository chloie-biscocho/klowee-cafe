import type { PublicEvent, PublicHome } from '../api/types'

export const HOME_URL = 'http://localhost:5181/api/public/home'

/** Everything empty: the state every section must survive. */
export const emptyHome: PublicHome = {
  settings: {},
  announcement: null,
  nextEvent: null,
  menu: null,
  packages: [],
  events: [],
}

export function event(overrides: Partial<PublicEvent> & Pick<PublicEvent, 'id' | 'name'>): PublicEvent {
  return {
    venue: 'Uptown Mall',
    startsOn: '2025-09-20',
    endsOn: '2025-09-20',
    status: 'Done',
    coverPhotoUrl: null,
    description: null,
    photos: [],
    ...overrides,
  }
}

export const fullHome: PublicHome = {
  settings: {
    hero_heading: 'smol pop-up cafe, big on matcha.',
    hero_body: 'Handcrafted coffee, matcha, and fruit sodas.',
    story_heading: 'Our story',
    story_body: 'First paragraph.\n\nSecond paragraph.',
    ticker_fallback: 'Now booking pop-ups around Cagayan de Oro',
    book_body: 'Tell us the date and headcount.',
    contact_email: 'hello@kloweecafe.com',
    instagram_url: 'https://instagram.com/kloweecafe',
    facebook_url: '',
  },
  announcement: null,
  nextEvent: {
    id: 'e-next',
    name: 'Uptown Night Market',
    venue: 'Uptown Mall',
    startsOn: '2099-05-21',
    endsOn: '2099-05-24',
    status: 'Upcoming',
    coverPhotoUrl: null,
  },
  menu: {
    versionName: 'April 2026 pop-up',
    effectiveFrom: '2026-04-01',
    categories: [
      {
        id: 'c-coffee',
        name: 'Coffee',
        items: [
          { id: 'i-1', name: 'Americano', description: '', price: 110, photoUrl: null, isFeatured: false },
          { id: 'i-2', name: 'Ube Latte', description: 'Purple and proud.', price: 160, photoUrl: null, isFeatured: true },
        ],
      },
      { id: 'c-empty', name: 'Seasonal', items: [] },
      {
        id: 'c-matcha',
        name: 'Matcha',
        items: [
          { id: 'i-3', name: 'Matcha Latte', description: '', price: 150, photoUrl: null, isFeatured: false },
        ],
      },
    ],
  },
  packages: [
    {
      id: 'p-1',
      name: 'Starter',
      price: 7500,
      description: '',
      guestCountNote: 'good for 50 pax',
      inclusions: ['Choose from 4 drinks', '2 baristas'],
    },
  ],
  events: [
    event({ id: 'e-past', name: 'First pop-up', description: 'Where it all started.' }),
    event({
      id: 'e-next',
      name: 'Uptown Night Market',
      startsOn: '2099-05-21',
      endsOn: '2099-05-24',
      status: 'Upcoming',
      photos: [{ url: 'https://example.test/a.jpg', caption: 'Setting up' }],
    }),
  ],
}
