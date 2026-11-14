import { useAppDispatch } from '../../app/hooks'
import { showToast } from './toastSlice'

/** Copies text; the toast says whether it worked (a browser may refuse). */
export function useCopy() {
  const dispatch = useAppDispatch()
  return async (text: string, what: string) => {
    try {
      await navigator.clipboard.writeText(text)
      dispatch(showToast('success', `${what} copied.`))
    } catch {
      dispatch(showToast('error', `Couldn't copy. Select the ${what.toLowerCase()} and copy it.`))
    }
  }
}
