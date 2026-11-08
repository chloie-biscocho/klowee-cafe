import { useState } from 'react'
import { Link } from 'react-router'
import { useGetMonthlyQuery, useGetOutstandingQuery, useGetSummaryQuery } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Table, Td, Th } from '../../../components/ui/Table'
import { monthRange, shiftMonth, todayInManila } from '../../../lib/dates'
import { formatDate, formatPeso } from '../../../lib/format'
import { SettleModal } from '../customers/SettleModal'
import { lastTwelveMonths } from './chartScale'
import { MonthlyChart } from './MonthlyChart'

const monthTitle = new Intl.DateTimeFormat('en-PH', { month: 'long', year: 'numeric', timeZone: 'UTC' })

export function DashboardPage() {
  const today = todayInManila()
  const [month, setMonth] = useState(monthRange(today)[0])
  const [from, to] = monthRange(month)
  const isCurrentMonth = month === monthRange(today)[0]

  const { data: summary } = useGetSummaryQuery({ from, to })
  const { data: outstanding } = useGetOutstandingQuery()
  const year = Number(today.slice(0, 4))
  const { data: thisYear } = useGetMonthlyQuery(year)
  const { data: lastYear } = useGetMonthlyQuery(year - 1)
  const [settling, setSettling] = useState<{ id: string; name: string; balance: number } | null>(null)

  const chartMonths = lastTwelveMonths(today).map(({ year: y, month: m, key, label }) => {
    const row = (y === year ? thisYear : lastYear)?.find((r) => r.month === m)
    return { key, label, revenue: row?.revenue ?? 0, collected: row?.collected ?? 0 }
  })

  const stats = [
    { label: 'Revenue', value: summary ? formatPeso(summary.revenue) : '…', note: 'orders this month' },
    { label: 'Collected', value: summary ? formatPeso(summary.collected) : '…', note: 'payments this month' },
    { label: 'Outstanding', value: summary ? formatPeso(summary.outstanding) : '…', note: 'all time' },
    { label: 'Drinks sold', value: summary ? String(summary.drinkCount) : '…', note: `${summary?.orderCount ?? 0} orders` },
  ]

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <Button size="sm" aria-label="Previous month" onClick={() => setMonth(shiftMonth(month, -1))}>
          ←
        </Button>
        <span className="min-w-36 text-center text-sm font-semibold">{monthTitle.format(new Date(`${month}T00:00:00Z`))}</span>
        <Button size="sm" aria-label="Next month" disabled={isCurrentMonth} onClick={() => setMonth(shiftMonth(month, 1))}>
          →
        </Button>
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {stats.map((stat) => (
          <Card key={stat.label} className="p-4">
            <p className="text-xs font-semibold text-muted uppercase">{stat.label}</p>
            <p className="mt-1 text-2xl font-bold tabular-nums" aria-label={stat.label}>
              {stat.value}
            </p>
            <p className="text-xs text-muted">{stat.note}</p>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]">
        <Card>
          <CardHeader>
            <h2 className="text-sm font-semibold">Outstanding tabs</h2>
          </CardHeader>
          {outstanding && outstanding.length === 0 ? (
            <EmptyState message="No open tabs. Everyone's paid up." />
          ) : (
            <Table>
              <thead>
                <tr>
                  <Th>Customer</Th>
                  <Th className="text-right">Balance</Th>
                  <Th>Oldest unpaid</Th>
                  <Th />
                </tr>
              </thead>
              <tbody>
                {outstanding?.map((row) => (
                  <tr key={row.customerId}>
                    <Td>
                      <Link className="hover:underline" to={`/sales/customers/${row.customerId}`}>
                        {row.name}
                      </Link>
                    </Td>
                    <Td className="text-right font-semibold">{formatPeso(row.balance)}</Td>
                    <Td className="text-muted">{formatDate(row.oldestUnpaidOn)}</Td>
                    <Td className="text-right">
                      <Button size="sm" onClick={() => setSettling({ id: row.customerId, name: row.name, balance: row.balance })}>
                        Settle
                      </Button>
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
          )}
        </Card>

        <Card>
          <CardHeader>
            <h2 className="text-sm font-semibold">Top drinks {isCurrentMonth ? 'this month' : 'that month'}</h2>
          </CardHeader>
          {summary && summary.topItems.length === 0 ? (
            <EmptyState message="No drinks sold this month yet." />
          ) : (
            <ol className="divide-y divide-line text-sm">
              {summary?.topItems.map((item, index) => (
                <li key={item.menuItemId} className="flex justify-between px-4 py-2">
                  <span>
                    <span className="mr-2 text-muted">{index + 1}.</span>
                    {item.name}
                  </span>
                  <span className="font-semibold tabular-nums">{item.quantity}</span>
                </li>
              ))}
            </ol>
          )}
        </Card>
      </div>

      <Card className="p-4">
        <h2 className="mb-3 text-sm font-semibold">Last twelve months</h2>
        <MonthlyChart months={chartMonths} />
      </Card>

      {settling && <SettleModal customer={settling} onClose={() => setSettling(null)} />}
    </div>
  )
}
