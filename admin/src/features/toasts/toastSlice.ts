import { createSlice, nanoid, type PayloadAction } from '@reduxjs/toolkit'

export type ToastTone = 'success' | 'error'

export interface Toast {
  id: string
  tone: ToastTone
  message: string
}

/**
 * Toasts live in the store rather than in a React context because the things
 * that raise them are not all components: the 401 handler inside `baseQuery`
 * needs to announce an expired session, and it only has `dispatch`.
 */
const toastSlice = createSlice({
  name: 'toasts',
  initialState: [] as Toast[],
  reducers: {
    showToast: {
      reducer(state, action: PayloadAction<Toast>) {
        state.push(action.payload)
      },
      prepare(tone: ToastTone, message: string) {
        return { payload: { id: nanoid(), tone, message } }
      },
    },
    dismissToast(state, action: PayloadAction<string>) {
      return state.filter((toast) => toast.id !== action.payload)
    },
  },
  selectors: {
    selectToasts: (state) => state,
  },
})

export const { showToast, dismissToast } = toastSlice.actions
export const { selectToasts } = toastSlice.selectors
export default toastSlice.reducer
