import { useState } from 'react'
import { Link } from 'react-router'
import { useApproveCardRequestMutation, useDeclineCardRequestMutation, useListCardRequestsQuery } from '../../../api/tabsApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Table, Td, Th } from '../../../components/ui/Table'
import { formatManilaDateTime } from '../../../lib/dates'
import { useToastedAction } from '../../toasts/useToastedAction'

/**
 * Cards customers asked for on their tab page (handover 17). Give card makes
 * it live from today at 0; Decline removes the request. Hidden when none wait.
 */
export function CardRequestsPanel() {
  const { data: requests } = useListCardRequestsQuery()
  const [approve] = useApproveCardRequestMutation()
  const [decline] = useDeclineCardRequestMutation()
  const run = useToastedAction()
  const [busy, setBusy] = useState<string | null>(null)

  if (!requests || requests.length === 0) return null

  const act = async (id: string, action: () => Promise<unknown>) => {
    setBusy(id)
    await action()
    setBusy(null)
  }

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold">Card requests</h2>
      </CardHeader>
      <Table>
        <thead>
          <tr>
            <Th>Customer</Th>
            <Th>Card</Th>
            <Th>Asked</Th>
            <Th />
          </tr>
        </thead>
        <tbody>
          {requests.map((request) => (
            <tr key={request.membershipId}>
              <Td>
                <Link className="hover:underline" to={`/sales/customers/${request.customerId}`}>
                  {request.customerName}
                </Link>
              </Td>
              <Td>{request.clubName}</Td>
              <Td className="text-muted">{formatManilaDateTime(request.requestedAt)}</Td>
              <Td className="text-right">
                <div className="flex justify-end gap-1.5">
                  <Button
                    size="sm"
                    variant="primary"
                    loading={busy === request.membershipId}
                    disabled={busy !== null}
                    onClick={() =>
                      act(request.membershipId, () =>
                        run(approve(request), `${request.customerName} has a ${request.clubName} card from today.`),
                      )
                    }
                  >
                    Give card
                  </Button>
                  <Button
                    size="sm"
                    disabled={busy !== null}
                    onClick={() => act(request.membershipId, () => run(decline(request), 'Request declined.'))}
                  >
                    Decline
                  </Button>
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  )
}
