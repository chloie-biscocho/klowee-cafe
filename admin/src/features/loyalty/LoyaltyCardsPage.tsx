import { useState } from 'react'
import { useCreateClubMutation, useListClubsQuery, useUpdateClubMutation } from '../../api/loyaltyApi'
import { useListAddOnsQuery, useListCategoriesQuery } from '../../api/menuApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { formatDate } from '../../lib/format'
import type { LoyaltyClubDto } from '../../types/api'
import { useToastedAction } from '../toasts/useToastedAction'
import { LoyaltyClubForm } from './LoyaltyClubForm'
import { toClubRequest } from './schemas'

/** Menu → Loyalty cards (decision 020): the clubs, their rewards, and which categories feed them. */
export function LoyaltyCardsPage() {
  const { data: clubs, isLoading } = useListClubsQuery()
  const { data: categories = [] } = useListCategoriesQuery()
  const { data: addOns = [] } = useListAddOnsQuery()
  const [createClub, { isLoading: isCreating }] = useCreateClubMutation()
  const [updateClub, { isLoading: isUpdating }] = useUpdateClubMutation()
  const run = useToastedAction()
  const [editing, setEditing] = useState<LoyaltyClubDto | 'new' | null>(null)

  const categoryNames = (club: LoyaltyClubDto) =>
    club.categoryIds.map((id) => categories.find((c) => c.id === id)?.name).filter(Boolean).join(', ')

  return (
    <>
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <p className="max-w-prose text-sm text-muted">
          Each card has a row of slots that paid drinks fill, one per drink, with rewards on some of them. After the
          last slot the card starts again; rewards not used yet stay available.
        </p>
        <Button variant="primary" onClick={() => setEditing('new')}>
          New card
        </Button>
      </div>

      <Card>
        <Table>
          <thead>
            <tr>
              <Th>Card</Th>
              <Th>Rewards</Th>
              <Th>Categories</Th>
              <Th className="text-right">Members</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={5} />}
            {clubs?.map((club) => (
              <tr key={club.id}>
                <Td className="font-medium">
                  {club.name}
                  <span className="ml-2">
                    {club.isActive ? <Badge tone="success">Active</Badge> : <Badge>Not active</Badge>}
                  </span>
                  <span className="block text-xs font-normal text-muted">
                    {club.style} · {club.slotCount} slots
                    {club.activatedOn && ` · since ${formatDate(club.activatedOn)}`}
                  </span>
                </Td>
                <Td className="text-sm">
                  {club.rewards.length === 0
                    ? '—'
                    : club.rewards.map((r) => `${r.slot}: ${r.label}`).join(' · ')}
                </Td>
                <Td className="text-sm text-muted">{categoryNames(club) || '—'}</Td>
                <Td className="text-right tabular-nums">{club.memberCount}</Td>
                <Td className="text-right">
                  <Button size="sm" onClick={() => setEditing(club)}>
                    Edit
                  </Button>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
        {!isLoading && (clubs?.length ?? 0) === 0 && <EmptyState message="No loyalty cards yet." />}
      </Card>

      {editing !== null && (
        <LoyaltyClubForm
          club={editing === 'new' ? null : editing}
          clubs={clubs ?? []}
          categories={categories}
          addOns={addOns}
          saving={isCreating || isUpdating}
          onClose={() => setEditing(null)}
          onSubmit={async (values) => {
            const sortOrder = editing === 'new' ? Math.max(0, ...(clubs ?? []).map((c) => c.sortOrder)) + 1 : editing.sortOrder
            const body = toClubRequest(values, sortOrder)
            const saved =
              editing === 'new'
                ? await run(createClub(body), 'Loyalty card created.')
                : await run(updateClub({ id: editing.id, body }), 'Loyalty card saved.')
            if (saved.ok) setEditing(null)
          }}
        />
      )}
    </>
  )
}
