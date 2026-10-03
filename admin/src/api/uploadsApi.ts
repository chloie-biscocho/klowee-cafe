import { baseApi } from './baseApi'
import type { UploadFolder, UploadResultDto } from '../types/api'

export const uploadsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    /**
     * `POST /uploads?folder=…` as multipart. The body is a `FormData`, which
     * `fetchBaseQuery` passes through untouched: the browser writes the
     * `multipart/form-data; boundary=…` header itself, so none is set here.
     */
    uploadImage: build.mutation<UploadResultDto, { file: File; folder: UploadFolder }>({
      query: ({ file, folder }) => {
        const body = new FormData()
        body.append('file', file)
        return { url: '/uploads', method: 'POST', params: { folder }, body }
      },
      // An upload changes no cached resource: the URL only matters once a save
      // stores it on an entity, and that save does the invalidating.
      invalidatesTags: [],
    }),
  }),
})

export const { useUploadImageMutation } = uploadsApi
