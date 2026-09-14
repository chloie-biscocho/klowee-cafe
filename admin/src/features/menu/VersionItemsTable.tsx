import { Button } from '../../components/ui/Button'
import { Table, Td, Th } from '../../components/ui/Table'
import { formatPeso } from '../../lib/format'
import { groupByCategory, type EditorItem } from './editorItems'

interface VersionItemsTableProps {
  items: EditorItem[]
  readOnly: boolean
  onChange: (index: number, patch: Partial<EditorItem>) => void
  onMove: (index: number, direction: -1 | 1) => void
  onRemove: (index: number) => void
}

export function VersionItemsTable({
  items,
  readOnly,
  onChange,
  onMove,
  onRemove,
}: VersionItemsTableProps) {
  const groups = groupByCategory(items)

  return (
    <Table>
      <thead>
        <tr>
          <Th className="w-[45%]">Item</Th>
          <Th className="w-28">Price</Th>
          <Th className="w-24">Available</Th>
          <Th className="w-24">Featured</Th>
          <Th className="text-right">Order</Th>
        </tr>
      </thead>
      {groups.map((group) => (
        <tbody key={group.category}>
          <tr>
            <Td colSpan={5} className="bg-canvas text-xs font-semibold tracking-wide text-muted uppercase">
              {group.category}
            </Td>
          </tr>
          {group.rows.map(({ item, index }, position) => (
            <tr key={item.menuItemId}>
              <Td className="font-medium">{item.name}</Td>
              <Td>
                {readOnly ? (
                  formatPeso(Number(item.price))
                ) : (
                  <input
                    type="number"
                    min="0"
                    step="0.5"
                    aria-label={`Price for ${item.name}`}
                    value={item.price}
                    onChange={(event) => onChange(index, { price: event.target.value })}
                    className="h-8 w-24 rounded-md border border-line bg-surface px-2 text-sm"
                  />
                )}
              </Td>
              <Td>
                <input
                  type="checkbox"
                  className="size-4 accent-accent"
                  aria-label={`${item.name} available`}
                  disabled={readOnly}
                  checked={item.isAvailable}
                  onChange={(event) => onChange(index, { isAvailable: event.target.checked })}
                />
              </Td>
              <Td>
                <input
                  type="checkbox"
                  className="size-4 accent-accent"
                  aria-label={`${item.name} featured`}
                  disabled={readOnly}
                  checked={item.isFeatured}
                  onChange={(event) => onChange(index, { isFeatured: event.target.checked })}
                />
              </Td>
              <Td className="text-right">
                <div className="flex justify-end gap-1">
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={`Move ${item.name} up`}
                    disabled={readOnly || position === 0}
                    onClick={() => onMove(index, -1)}
                  >
                    &uarr;
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={`Move ${item.name} down`}
                    disabled={readOnly || position === group.rows.length - 1}
                    onClick={() => onMove(index, 1)}
                  >
                    &darr;
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    disabled={readOnly}
                    onClick={() => onRemove(index)}
                  >
                    Remove
                  </Button>
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      ))}
    </Table>
  )
}
