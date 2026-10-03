import { baseApi, LIST_ID, listTags } from './baseApi'
import type { PackageDto, PackageRequest } from '../types/api'

export const packagesApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listPackages: build.query<PackageDto[], { includeInactive: boolean }>({
      query: ({ includeInactive }) => ({ url: '/packages', params: { includeInactive } }),
      providesTags: (rows) => listTags('Package', rows),
    }),
    createPackage: build.mutation<PackageDto, PackageRequest>({
      query: (body) => ({ url: '/packages', method: 'POST', body }),
      invalidatesTags: [{ type: 'Package', id: LIST_ID }],
    }),
    /** Full replacement: the body carries the package's complete inclusion list. */
    updatePackage: build.mutation<PackageDto, { id: string; body: PackageRequest }>({
      query: ({ id, body }) => ({ url: `/packages/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Package', id },
        { type: 'Package', id: LIST_ID },
      ],
    }),
    setPackageActive: build.mutation<PackageDto, { id: string; isActive: boolean }>({
      query: ({ id, isActive }) => ({
        url: `/packages/${id}/${isActive ? 'activate' : 'deactivate'}`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Package', id },
        { type: 'Package', id: LIST_ID },
      ],
    }),
    deletePackage: build.mutation<void, string>({
      query: (id) => ({ url: `/packages/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Package', id: LIST_ID }],
    }),
  }),
})

export const {
  useListPackagesQuery,
  useCreatePackageMutation,
  useUpdatePackageMutation,
  useSetPackageActiveMutation,
  useDeletePackageMutation,
} = packagesApi
