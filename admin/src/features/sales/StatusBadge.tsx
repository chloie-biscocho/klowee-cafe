import { Badge } from '../../components/ui/Badge'
import type { PaymentStatus } from '../../types/api'

const TONES = { Unpaid: 'danger', Partial: 'warning', Paid: 'success' } as const

export function StatusBadge({ status }: { status: PaymentStatus }) {
  return <Badge tone={TONES[status]}>{status}</Badge>
}
