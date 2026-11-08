import { useState } from 'react'
import { useNavigate } from 'react-router'
import {
  useCreateCustomerMutation,
  useDeleteCustomerMutation,
  useListCustomersQuery,
  useSetCustomerActiveMutation,
  useUpdateCustomerMutation,
} from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { EmptyState } from '../../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../../components/ui/Table'
import { formatDate, formatPeso } from '../../../lib/format'
import type { CustomerSummaryDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { blankToNull, type CustomerValues } from '../schemas'
import { CustomerFormModal } from './CustomerFormModal'

export function CustomersPage() {
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const { data: customers, isLoading, isFetching } = useListCustomersQuery({ includeInactive })
  const [createCustomer, { isLoading: isCreating }] = useCreateCustomerMutation()
  const [updateCustomer, { isLoading: isUpdating }] = useUpdateCustomerMutation()
  const [setActive] = useSetCustomerActiveMutation()
  const [deleteCustomer, { isLoading: isDeleting }] = useDeleteCustomerMutation()
  const run = useToastedAction()
  const navigate = useNavigate()

  // `draft | null`: what the form is editing, or nothing (decision 007).
  const [editing, setEditing] = useState<CustomerSummaryDto | 'new' | null>(null)
  const [pendingDelete, setPendingDelete] = useState<CustomerSummaryDto | null>(null)

  const term = search.trim().toLowerCase()
  const rows = (customers ?? []).filter(
    (c) => term === '' || `${c.name} ${c.nickname ?? ''}`.toLowerCase().includes(term),
  )

  const save = async (values: CustomerValues) => {
    const body = { name: values.name, nickname: blankToNull(values.nickname), notes: blankToNull(values.notes), isActive: true }
    const outcome =
      editing === 'new' || editing === null
        ? await run(createCustomer(body), 'Customer added.')
        : await run(updateCustomer({ id: editing.id, body: { ...body, isActive: editing.isActive } }), 'Customer saved.')
    if (outcome.ok) setEditing(null)
  }

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center gap-4">
            <input
              aria-label="Search customers"
              placeholder="Search name or nickname"
              className="h-8 w-56 rounded-md border border-line px-2.5 text-sm"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
            <label className="flex items-center gap-2 text-sm text-muted">
              <input
                type="checkbox"
                className="size-4 accent-accent"
                checked={includeInactive}
                onChange={(event) => setIncludeInactive(event.target.checked)}
              />
              Show inactive
            </label>
          </div>
          <Button variant="primary" size="sm" onClick={() => setEditing('new')}>
            New customer
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th>Name</Th>
              <Th className="text-right">Tab</Th>
              <Th className="text-right">Stamps</Th>
              <Th className="text-right">Rewards</Th>
              <Th>Last order</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={6} />}
            {rows.map((customer) => (
              <tr
                key={customer.id}
                className={`cursor-pointer hover:bg-canvas ${isFetching ? 'opacity-60' : ''}`}
                onClick={() => navigate(`/sales/customers/${customer.id}`)}
              >
                <Td className="font-medium">
                  {customer.name}
                  {customer.nickname && <span className="ml-2 text-xs font-normal text-muted">{customer.nickname}</span>}
                  {!customer.isActive && <span className="ml-2 text-xs font-normal text-muted">(inactive)</span>}
                </Td>
                <Td className={`text-right ${customer.outstandingBalance > 0 ? 'font-semibold' : 'text-muted'}`}>
                  {formatPeso(customer.outstandingBalance)}
                </Td>
                <Td className="text-right">{customer.loyaltyBalance}</Td>
                <Td className="text-right">{customer.availableRewards || '—'}</Td>
                <Td className="text-muted">{customer.lastOrderOn ? formatDate(customer.lastOrderOn) : '—'}</Td>
                <Td className="text-right" onClick={(event) => event.stopPropagation()}>
                  <div className="flex justify-end gap-1.5">
                    <Button size="sm" onClick={() => setEditing(customer)}>
                      Edit
                    </Button>
                    <Button
                      size="sm"
                      onClick={() =>
                        run(
                          setActive({ id: customer.id, isActive: !customer.isActive }),
                          customer.isActive ? 'Customer deactivated.' : 'Customer activated.',
                        )
                      }
                    >
                      {customer.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => setPendingDelete(customer)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
        {!isLoading && rows.length === 0 && (
          <EmptyState message={term ? 'No customer matches that search.' : 'No customers yet.'} />
        )}
      </Card>

      {editing && (
        <CustomerFormModal
          key={editing === 'new' ? 'new' : editing.id}
          customer={editing === 'new' ? null : editing}
          saving={isCreating || isUpdating}
          onClose={() => setEditing(null)}
          onSubmit={save}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete customer"
        message={`Delete ${pendingDelete?.name}? Customers with orders can't be deleted; deactivate them instead.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          await run(deleteCustomer(pendingDelete.id), 'Customer deleted.')
          setPendingDelete(null)
        }}
      />
    </>
  )
}
