import { baseApi, LIST_ID } from './baseApi'
import type {
  AddOnDto,
  AddOnRequest,
  CreateMenuVersionRequest,
  MenuCategoryDto,
  MenuCategoryRequest,
  MenuContext,
  MenuItemDto,
  MenuItemRequest,
  MenuVersionDetailDto,
  MenuVersionItemRequest,
  MenuVersionSummaryDto,
  UpdateMenuVersionRequest,
} from '../types/api'

/** `[{ type, id: LIST }, ...one tag per row]` — the standard RTK Query list shape. */
function listTags<T extends { id: string }>(
  type: 'Category' | 'Item' | 'AddOn' | 'Version',
  rows: T[] | undefined,
) {
  return [
    { type, id: LIST_ID } as const,
    ...(rows ?? []).map((row) => ({ type, id: row.id }) as const),
  ]
}

export const menuApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // ---- Categories ----
    listCategories: build.query<MenuCategoryDto[], void>({
      query: () => '/menu/categories',
      providesTags: (rows) => listTags('Category', rows),
    }),
    createCategory: build.mutation<MenuCategoryDto, MenuCategoryRequest>({
      query: (body) => ({ url: '/menu/categories', method: 'POST', body }),
      invalidatesTags: [{ type: 'Category', id: LIST_ID }],
    }),
    updateCategory: build.mutation<MenuCategoryDto, { id: string; body: MenuCategoryRequest }>({
      query: ({ id, body }) => ({ url: `/menu/categories/${id}`, method: 'PUT', body }),
      // Items carry `categoryName`, and so does every version item row, so a
      // rename restates both. A bare type invalidates every id of that type.
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Category', id },
        { type: 'Category', id: LIST_ID },
        { type: 'Item', id: LIST_ID },
        'VersionDetail',
      ],
    }),
    deleteCategory: build.mutation<void, string>({
      query: (id) => ({ url: `/menu/categories/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Category', id: LIST_ID }],
    }),

    // ---- Items ----
    listItems: build.query<MenuItemDto[], { includeArchived: boolean }>({
      query: ({ includeArchived }) => ({
        url: '/menu/items',
        params: { includeArchived },
      }),
      providesTags: (rows) => listTags('Item', rows),
    }),
    createItem: build.mutation<MenuItemDto, MenuItemRequest>({
      query: (body) => ({ url: '/menu/items', method: 'POST', body }),
      invalidatesTags: [{ type: 'Item', id: LIST_ID }],
    }),
    updateItem: build.mutation<MenuItemDto, { id: string; body: MenuItemRequest }>({
      query: ({ id, body }) => ({ url: `/menu/items/${id}`, method: 'PUT', body }),
      // A renamed item shows up inside every version's item table too.
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Item', id },
        { type: 'Item', id: LIST_ID },
        'VersionDetail',
      ],
    }),
    setItemArchived: build.mutation<MenuItemDto, { id: string; isArchived: boolean }>({
      query: ({ id, isArchived }) => ({
        url: `/menu/items/${id}/${isArchived ? 'archive' : 'unarchive'}`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Item', id },
        { type: 'Item', id: LIST_ID },
      ],
    }),
    deleteItem: build.mutation<void, string>({
      query: (id) => ({ url: `/menu/items/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Item', id: LIST_ID }],
    }),

    // ---- Add-ons ----
    listAddOns: build.query<AddOnDto[], void>({
      query: () => '/menu/add-ons',
      providesTags: (rows) => listTags('AddOn', rows),
    }),
    createAddOn: build.mutation<AddOnDto, AddOnRequest>({
      query: (body) => ({ url: '/menu/add-ons', method: 'POST', body }),
      invalidatesTags: [{ type: 'AddOn', id: LIST_ID }],
    }),
    updateAddOn: build.mutation<AddOnDto, { id: string; body: AddOnRequest }>({
      query: ({ id, body }) => ({ url: `/menu/add-ons/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'AddOn', id },
        { type: 'AddOn', id: LIST_ID },
      ],
    }),
    deleteAddOn: build.mutation<void, string>({
      query: (id) => ({ url: `/menu/add-ons/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'AddOn', id: LIST_ID }],
    }),

    // ---- Versions ----
    listVersions: build.query<MenuVersionSummaryDto[], { context?: MenuContext }>({
      query: ({ context }) => ({ url: '/menu/versions', params: context ? { context } : {} }),
      providesTags: (rows) => listTags('Version', rows),
    }),
    getVersion: build.query<MenuVersionDetailDto, string>({
      query: (id) => `/menu/versions/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'VersionDetail', id }],
    }),
    createVersion: build.mutation<MenuVersionDetailDto, CreateMenuVersionRequest>({
      query: (body) => ({ url: '/menu/versions', method: 'POST', body }),
      invalidatesTags: [{ type: 'Version', id: LIST_ID }],
    }),
    updateVersion: build.mutation<
      MenuVersionDetailDto,
      { id: string; body: UpdateMenuVersionRequest }
    >({
      query: ({ id, body }) => ({ url: `/menu/versions/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Version', id },
        { type: 'Version', id: LIST_ID },
        { type: 'VersionDetail', id },
      ],
    }),
    setVersionPublished: build.mutation<
      MenuVersionDetailDto,
      { id: string; isPublished: boolean }
    >({
      query: ({ id, isPublished }) => ({
        url: `/menu/versions/${id}/${isPublished ? 'publish' : 'unpublish'}`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Version', id },
        { type: 'Version', id: LIST_ID },
        { type: 'VersionDetail', id },
      ],
    }),
    /** Full replacement: the body is the version's complete item list. */
    replaceVersionItems: build.mutation<
      MenuVersionDetailDto,
      { id: string; items: MenuVersionItemRequest[] }
    >({
      query: ({ id, items }) => ({ url: `/menu/versions/${id}/items`, method: 'PUT', body: items }),
      // The list row shows an item count, so it is stale too.
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'VersionDetail', id },
        { type: 'Version', id },
        { type: 'Version', id: LIST_ID },
      ],
    }),
    deleteVersion: build.mutation<void, string>({
      query: (id) => ({ url: `/menu/versions/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Version', id: LIST_ID }],
    }),
  }),
})

export const {
  useListCategoriesQuery,
  useCreateCategoryMutation,
  useUpdateCategoryMutation,
  useDeleteCategoryMutation,
  useListItemsQuery,
  useCreateItemMutation,
  useUpdateItemMutation,
  useSetItemArchivedMutation,
  useDeleteItemMutation,
  useListAddOnsQuery,
  useCreateAddOnMutation,
  useUpdateAddOnMutation,
  useDeleteAddOnMutation,
  useListVersionsQuery,
  useGetVersionQuery,
  useCreateVersionMutation,
  useUpdateVersionMutation,
  useSetVersionPublishedMutation,
  useReplaceVersionItemsMutation,
  useDeleteVersionMutation,
} = menuApi
