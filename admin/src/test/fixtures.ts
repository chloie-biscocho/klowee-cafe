import type {
  EventDetailDto,
  EventSummaryDto,
  LoginResponse,
  MenuItemDto,
  MenuVersionDetailDto,
  MenuVersionSummaryDto,
  PackageDto,
  SiteSettingDto,
  UserDto,
} from '../types/api'

export const API = 'http://localhost:5181/api'

export const owner: UserDto = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'klowe.cafe@gmail.com',
  displayName: 'Klowee Owner',
}

export const loginResponse: LoginResponse = {
  accessToken: 'test.access.token',
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: owner,
}

/** Auth state for a test that starts signed in. */
export const signedIn = {
  auth: {
    token: loginResponse.accessToken,
    user: owner,
    expiresAt: loginResponse.expiresAt,
  },
}

export const draftVersion: MenuVersionSummaryDto = {
  id: 'aaaaaaaa-0000-0000-0000-000000000001',
  name: 'May 2026 pop-up',
  context: 'PopUp',
  effectiveFrom: '2026-05-01',
  isPublished: false,
  notes: null,
  itemCount: 2,
}

export const publishedVersion: MenuVersionSummaryDto = {
  id: 'aaaaaaaa-0000-0000-0000-000000000002',
  name: 'April 2026 pop-up',
  context: 'PopUp',
  effectiveFrom: '2026-04-01',
  isPublished: true,
  notes: null,
  itemCount: 13,
}

export const draftVersionDetail: MenuVersionDetailDto = {
  ...draftVersion,
  items: [
    {
      id: 'bbbbbbbb-0000-0000-0000-000000000001',
      menuItemId: 'cccccccc-0000-0000-0000-000000000001',
      name: 'Ube Latte',
      categoryName: 'Coffee',
      price: 150,
      isAvailable: true,
      isFeatured: false,
      sortOrder: 0,
    },
    {
      id: 'bbbbbbbb-0000-0000-0000-000000000002',
      menuItemId: 'cccccccc-0000-0000-0000-000000000002',
      name: 'Matcha Latte',
      categoryName: 'Coffee',
      price: 165,
      isAvailable: true,
      isFeatured: true,
      sortOrder: 1,
    },
  ],
}

export const menuItems: MenuItemDto[] = [
  {
    id: 'cccccccc-0000-0000-0000-000000000001',
    name: 'Ube Latte',
    description: '',
    categoryId: 'dddddddd-0000-0000-0000-000000000001',
    categoryName: 'Coffee',
    photoUrl: null,
    isArchived: false,
  },
]

export const corporatePackage: PackageDto = {
  id: 'eeeeeeee-0000-0000-0000-000000000001',
  name: 'Corporate cart',
  price: 15000,
  description: '',
  guestCountNote: 'Good for 60 pax',
  isActive: true,
  sortOrder: 0,
  inclusions: [
    { id: 'eeeeeeee-1111-0000-0000-000000000001', text: '2 baristas', sortOrder: 0 },
    { id: 'eeeeeeee-1111-0000-0000-000000000002', text: '3 hours of service', sortOrder: 1 },
    { id: 'eeeeeeee-1111-0000-0000-000000000003', text: '60 cups', sortOrder: 2 },
  ],
}

const PHOTO = 'https://example.supabase.co/storage/v1/object/public/media/events/2026/05'

export const upcomingEvent: EventDetailDto = {
  id: 'ffffffff-0000-0000-0000-000000000001',
  name: 'Uptown Night Market',
  venue: 'Uptown Mall',
  address: null,
  startsOn: '2099-05-21',
  endsOn: '2099-05-24',
  description: null,
  coverPhotoUrl: null,
  status: 'Upcoming',
  isPublished: false,
  photos: [
    { id: 'ffffffff-1111-0000-0000-000000000001', url: `${PHOTO}/a.png`, caption: 'Setting up', sortOrder: 0 },
    { id: 'ffffffff-1111-0000-0000-000000000002', url: `${PHOTO}/b.png`, caption: null, sortOrder: 1 },
  ],
}

export function eventSummary(event: EventDetailDto): EventSummaryDto {
  return {
    id: event.id,
    name: event.name,
    venue: event.venue,
    startsOn: event.startsOn,
    endsOn: event.endsOn,
    status: event.status,
    isPublished: event.isPublished,
    coverPhotoUrl: event.coverPhotoUrl,
    photoCount: event.photos.length,
  }
}

export const siteSettings: SiteSettingDto[] = [
  { key: 'contact_email', value: 'klowee.cafe@gmail.com' },
  { key: 'hero_heading', value: 'Coffee that comes to you' },
  { key: 'hero_body', value: 'A mobile coffee bar in Cagayan de Oro.' },
  { key: 'instagram_url', value: 'https://instagram.com/klowee.cafe' },
  { key: 'ticker_fallback', value: 'Book us for your next event.' },
]
