import { baseApi, LIST_ID, listTags } from './baseApi'
import type {
  CustomerDetailDto,
  CustomerRequest,
  CustomerSummaryDto,
  LoyaltyAdjustRequest,
  LoyaltyEntryDto,
  MenuVersionDetailDto,
  MonthlyRowDto,
  OrderDetailDto,
  OrderRequest,
  OrderSummaryDto,
  OutstandingRowDto,
  PagedResult,
  PaymentDto,
  PaymentRequest,
  PaymentStatus,
  SummaryReportDto,
} from '../types/api'

export interface OrderListArgs {
  from?: string
  to?: string
  customerId?: string
  status?: PaymentStatus
  page: number
  pageSize: number
}

/**
 * What a change to money touches (decision 017). An order or a payment moves
 * the order, both people's tabs (drinker and payer) and recent-orders lists,
 * the stamp ledger, and every report; so those mutations invalidate whole
 * tag types rather than guessing which customer ids are involved.
 */
const MONEY_CHANGED = ['Customer', 'Loyalty', 'Report'] as const

export const salesApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    // ---- Customers ----
    listCustomers: build.query<CustomerSummaryDto[], { includeInactive: boolean; q?: string }>({
      query: ({ includeInactive, q }) => ({ url: '/customers', params: q ? { includeInactive, q } : { includeInactive } }),
      providesTags: (rows) => listTags('Customer', rows),
    }),
    getCustomer: build.query<CustomerDetailDto, string>({
      query: (id) => `/customers/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Customer', id }],
    }),
    createCustomer: build.mutation<CustomerDetailDto, CustomerRequest>({
      query: (body) => ({ url: '/customers', method: 'POST', body }),
      invalidatesTags: [{ type: 'Customer', id: LIST_ID }],
    }),
    /** Names appear in order lists and the outstanding report, so those refresh too. */
    updateCustomer: build.mutation<CustomerDetailDto, { id: string; body: CustomerRequest }>({
      query: ({ id, body }) => ({ url: `/customers/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Customer', id },
        { type: 'Customer', id: LIST_ID },
        'Order',
        'Report',
      ],
    }),
    setCustomerActive: build.mutation<CustomerDetailDto, { id: string; isActive: boolean }>({
      query: ({ id, isActive }) => ({ url: `/customers/${id}/${isActive ? 'activate' : 'deactivate'}`, method: 'PATCH' }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Customer', id },
        { type: 'Customer', id: LIST_ID },
      ],
    }),
    deleteCustomer: build.mutation<void, string>({
      query: (id) => ({ url: `/customers/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Customer', id: LIST_ID }],
    }),
    mergeCustomer: build.mutation<CustomerDetailDto, { id: string; targetId: string }>({
      query: ({ id, targetId }) => ({ url: `/customers/${id}/merge-into/${targetId}`, method: 'POST' }),
      invalidatesTags: ['Order', ...MONEY_CHANGED],
    }),
    getLoyalty: build.query<LoyaltyEntryDto[], string>({
      query: (id) => `/customers/${id}/loyalty`,
      providesTags: (_r, _e, id) => [{ type: 'Loyalty', id }],
    }),
    adjustStamps: build.mutation<LoyaltyEntryDto, { id: string; body: LoyaltyAdjustRequest }>({
      query: ({ id, body }) => ({ url: `/customers/${id}/loyalty/adjust`, method: 'POST', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Customer', id },
        { type: 'Customer', id: LIST_ID },
        { type: 'Loyalty', id },
      ],
    }),
    settleTab: build.mutation<PaymentDto[], { id: string; body: PaymentRequest }>({
      query: ({ id, body }) => ({ url: `/customers/${id}/settle`, method: 'POST', body }),
      invalidatesTags: ['Order', ...MONEY_CHANGED],
    }),

    // ---- Orders and payments ----
    listOrders: build.query<PagedResult<OrderSummaryDto>, OrderListArgs>({
      query: (args) => ({
        url: '/orders',
        params: Object.fromEntries(Object.entries(args).filter(([, value]) => value !== undefined && value !== '')),
      }),
      providesTags: (result) => listTags('Order', result?.items),
    }),
    getOrder: build.query<OrderDetailDto, string>({
      query: (id) => `/orders/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Order', id }],
    }),
    createOrder: build.mutation<OrderDetailDto, OrderRequest>({
      query: (body) => ({ url: '/orders', method: 'POST', body }),
      invalidatesTags: [{ type: 'Order', id: LIST_ID }, ...MONEY_CHANGED],
    }),
    updateOrder: build.mutation<OrderDetailDto, { id: string; body: OrderRequest }>({
      query: ({ id, body }) => ({ url: `/orders/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [{ type: 'Order', id }, { type: 'Order', id: LIST_ID }, ...MONEY_CHANGED],
    }),
    deleteOrder: build.mutation<void, string>({
      query: (id) => ({ url: `/orders/${id}`, method: 'DELETE' }),
      invalidatesTags: (_r, _e, id) => [{ type: 'Order', id }, { type: 'Order', id: LIST_ID }, ...MONEY_CHANGED],
    }),
    addPayment: build.mutation<OrderDetailDto, { orderId: string; body: PaymentRequest }>({
      query: ({ orderId, body }) => ({ url: `/orders/${orderId}/payments`, method: 'POST', body }),
      invalidatesTags: (_r, _e, { orderId }) => [
        { type: 'Order', id: orderId },
        { type: 'Order', id: LIST_ID },
        ...MONEY_CHANGED,
      ],
    }),
    deletePayment: build.mutation<void, { orderId: string; paymentId: string }>({
      query: ({ orderId, paymentId }) => ({ url: `/orders/${orderId}/payments/${paymentId}`, method: 'DELETE' }),
      invalidatesTags: (_r, _e, { orderId }) => [
        { type: 'Order', id: orderId },
        { type: 'Order', id: LIST_ID },
        ...MONEY_CHANGED,
      ],
    }),

    // ---- What orders sell from ----
    /** The menu new orders are taken from. 404 when none is published. Publishing a version refreshes it. */
    getCurrentOfficeMenu: build.query<MenuVersionDetailDto, void>({
      query: () => ({ url: '/menu/versions/current', params: { context: 'Office' } }),
      providesTags: [{ type: 'Version', id: LIST_ID }],
    }),

    // ---- Reports ----
    getSummary: build.query<SummaryReportDto, { from: string; to: string }>({
      query: (params) => ({ url: '/reports/summary', params }),
      providesTags: ['Report'],
    }),
    getOutstanding: build.query<OutstandingRowDto[], void>({
      query: () => '/reports/outstanding',
      providesTags: ['Report'],
    }),
    getMonthly: build.query<MonthlyRowDto[], number>({
      query: (year) => ({ url: '/reports/monthly', params: { year } }),
      providesTags: ['Report'],
    }),
  }),
})

export const {
  useListCustomersQuery,
  useGetCustomerQuery,
  useCreateCustomerMutation,
  useUpdateCustomerMutation,
  useSetCustomerActiveMutation,
  useDeleteCustomerMutation,
  useMergeCustomerMutation,
  useGetLoyaltyQuery,
  useAdjustStampsMutation,
  useSettleTabMutation,
  useListOrdersQuery,
  useGetOrderQuery,
  useCreateOrderMutation,
  useUpdateOrderMutation,
  useDeleteOrderMutation,
  useAddPaymentMutation,
  useDeletePaymentMutation,
  useGetCurrentOfficeMenuQuery,
  useGetSummaryQuery,
  useGetOutstandingQuery,
  useGetMonthlyQuery,
} = salesApi
