import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { Spinner } from '../../components/ui/Spinner'
import type { MenuItemDto } from '../../types/api'

interface AddItemPickerProps {
  open: boolean
  loading: boolean
  /** Non-archived items that are not already priced on this version. */
  candidates: MenuItemDto[]
  onClose: () => void
  onAdd: (items: MenuItemDto[]) => void
}

export function AddItemPicker({ open, loading, candidates, onClose, onAdd }: AddItemPickerProps) {
  const [selected, setSelected] = useState<string[]>([])

  const toggle = (id: string) =>
    setSelected((current) =>
      current.includes(id) ? current.filter((value) => value !== id) : [...current, id],
    )

  return (
    <Modal
      open={open}
      title="Add items to this version"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Cancel</Button>
          <Button
            variant="primary"
            disabled={selected.length === 0}
            onClick={() => onAdd(candidates.filter((item) => selected.includes(item.id)))}
          >
            Add {selected.length > 0 ? selected.length : ''} item
            {selected.length === 1 ? '' : 's'}
          </Button>
        </>
      }
    >
      {loading && <Spinner className="size-5 text-muted" />}

      {!loading && candidates.length === 0 && (
        <p className="py-4 text-sm text-muted">
          Every active menu item is already on this version.
        </p>
      )}

      <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto">
        {candidates.map((item) => (
          <li key={item.id}>
            <label className="flex cursor-pointer items-center gap-2.5 rounded-md px-2 py-1.5 text-sm hover:bg-canvas">
              <input
                type="checkbox"
                className="size-4 accent-accent"
                checked={selected.includes(item.id)}
                onChange={() => toggle(item.id)}
              />
              <span className="flex-1">{item.name}</span>
              <span className="text-xs text-muted">{item.categoryName}</span>
            </label>
          </li>
        ))}
      </ul>
    </Modal>
  )
}
