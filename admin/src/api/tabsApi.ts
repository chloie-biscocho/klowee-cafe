import { baseApi, LIST_ID, listTags } from './baseApi'
import { MONEY_CHANGED } from './salesApi'
import type {
  CardRequestDto,
  ConfirmClaimRequest,
  PaymentClaimDto,
  PaymentClaimStatus,
  PaymentMethodDto,
  PaymentMethodRequest,
  RejectClaimRequest,
  TabLinkDto,
} from '../types/api'

/**
 * Customers' private tab pages (handover 17, decision 021): their links, the
 * payments they report, the accounts they pay into, and the cards they ask for.
 */
export const tabsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // ---- Links ----
    /** Creates the link, or replaces it: the old URL stops working at once. */
    createTabLink: build.mutation<TabLinkDto, string>({
      query: (customerId) => ({ url: `/customers/${customerId}/tab-link`, method: 'POST' }),
      invalidatesTags: (_r, _e, id) => [
        { type: 'Customer', id },
        { type: 'Customer', id: LIST_ID },
      ],
    }),

    // ---- Payment claims ----
    listPaymentClaims: build.query<PaymentClaimDto[], PaymentClaimStatus | undefined>({
      query: (status) => ({ url: '/payment-claims', params: status ? { status } : undefined }),
      providesTags: (rows) => listTags('PaymentClaim', rows),
    }),
    /** Settles the payer's tab with the claim: everything a payment touches, plus the claims. */
    confirmClaim: build.mutation<PaymentClaimDto, { id: string; body: ConfirmClaimRequest }>({
      query: ({ id, body }) => ({ url: `/payment-claims/${id}/confirm`, method: 'POST', body }),
      invalidatesTags: ['Order', ...MONEY_CHANGED, 'PaymentClaim'],
    }),
    /** Moves no money; the dashboard's count changes. */
    rejectClaim: build.mutation<PaymentClaimDto, { id: string; body: RejectClaimRequest }>({
      query: ({ id, body }) => ({ url: `/payment-claims/${id}/reject`, method: 'POST', body }),
      invalidatesTags: ['PaymentClaim', 'Report'],
    }),

    // ---- Payment methods ----
    listPaymentMethods: build.query<PaymentMethodDto[], void>({
      query: () => '/payment-methods',
      providesTags: (rows) => listTags('PaymentMethod', rows),
    }),
    createPaymentMethod: build.mutation<PaymentMethodDto, PaymentMethodRequest>({
      query: (body) => ({ url: '/payment-methods', method: 'POST', body }),
      invalidatesTags: [{ type: 'PaymentMethod', id: LIST_ID }],
    }),
    updatePaymentMethod: build.mutation<PaymentMethodDto, { id: string; body: PaymentMethodRequest }>({
      query: ({ id, body }) => ({ url: `/payment-methods/${id}`, method: 'PUT', body }),
      invalidatesTags: [{ type: 'PaymentMethod', id: LIST_ID }],
    }),
    setPaymentMethodActive: build.mutation<PaymentMethodDto, { id: string; isActive: boolean }>({
      query: ({ id, isActive }) => ({ url: `/payment-methods/${id}/${isActive ? 'activate' : 'deactivate'}`, method: 'PATCH' }),
      invalidatesTags: [{ type: 'PaymentMethod', id: LIST_ID }],
    }),
    deletePaymentMethod: build.mutation<void, string>({
      query: (id) => ({ url: `/payment-methods/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'PaymentMethod', id: LIST_ID }],
    }),

    // ---- Card requests ----
    listCardRequests: build.query<CardRequestDto[], void>({
      query: () => '/loyalty/card-requests',
      providesTags: [{ type: 'CardRequest', id: LIST_ID }],
    }),
    /** Gives the card from today: the customer's cards and the clubs' counts change. */
    approveCardRequest: build.mutation<void, CardRequestDto>({
      query: (request) => ({ url: `/loyalty/memberships/${request.membershipId}/approve`, method: 'POST' }),
      invalidatesTags: (_r, _e, request) => [
        { type: 'CardRequest', id: LIST_ID },
        { type: 'Customer', id: request.customerId },
        { type: 'Customer', id: LIST_ID },
        'LoyaltyClub',
        'Report',
      ],
    }),
    declineCardRequest: build.mutation<void, CardRequestDto>({
      query: (request) => ({ url: `/loyalty/memberships/${request.membershipId}/decline`, method: 'POST' }),
      invalidatesTags: (_r, _e, request) => [
        { type: 'CardRequest', id: LIST_ID },
        { type: 'Customer', id: request.customerId },
        'Report',
      ],
    }),
  }),
})

export const {
  useCreateTabLinkMutation,
  useListPaymentClaimsQuery,
  useConfirmClaimMutation,
  useRejectClaimMutation,
  useListPaymentMethodsQuery,
  useCreatePaymentMethodMutation,
  useUpdatePaymentMethodMutation,
  useSetPaymentMethodActiveMutation,
  useDeletePaymentMethodMutation,
  useListCardRequestsQuery,
  useApproveCardRequestMutation,
  useDeclineCardRequestMutation,
} = tabsApi
