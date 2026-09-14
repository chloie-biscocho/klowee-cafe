import type {
  LoginResponse,
  MenuItemDto,
  MenuVersionDetailDto,
  MenuVersionSummaryDto,
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
