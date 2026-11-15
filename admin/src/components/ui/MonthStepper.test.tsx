import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { MonthStepper } from './MonthStepper'

const TODAY = '2026-11-15'

describe('MonthStepper (handover 18)', () => {
  it('names the month, steps back, and disables Next on the current month', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    render(<MonthStepper month="2026-11-01" onChange={onChange} today={TODAY} />)

    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent('November 2026')
    expect(screen.getByRole('button', { name: 'Next month' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Previous month' }))
    expect(onChange).toHaveBeenCalledWith('2026-10-01')
  })

  it('steps forward from an earlier month, across a year', async () => {
    const onChange = vi.fn()
    const user = userEvent.setup()
    render(<MonthStepper month="2025-12-01" onChange={onChange} today={TODAY} />)

    expect(screen.getByText('December 2025')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Next month' }))
    expect(onChange).toHaveBeenCalledWith('2026-01-01')
  })

  it("shows a replacement label (Orders' Custom)", () => {
    render(<MonthStepper month="2026-10-01" onChange={() => {}} today={TODAY} display="Custom" />)
    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent('Custom')
  })

  it('arrow keys step while it has focus, never past the current month, and focus stays on the control', async () => {
    function Stateful() {
      const [month, setMonth] = useState('2026-10-01')
      return <MonthStepper month={month} onChange={setMonth} today={TODAY} />
    }
    const user = userEvent.setup()
    render(<Stateful />)

    screen.getByRole('button', { name: 'Previous month' }).focus()
    await user.keyboard('{ArrowLeft}')
    expect(screen.getByText('September 2026')).toBeInTheDocument()
    await user.keyboard('{ArrowRight}{ArrowRight}')
    expect(screen.getByText('November 2026')).toBeInTheDocument()
    await user.keyboard('{ArrowRight}')
    expect(screen.getByText('November 2026')).toBeInTheDocument()

    // Next disables when it reaches the current month under the keyboard: focus moves to Previous, not the page.
    await user.click(screen.getByRole('button', { name: 'Previous month' }))
    screen.getByRole('button', { name: 'Next month' }).focus()
    await user.keyboard('{Enter}')
    expect(screen.getByRole('button', { name: 'Next month' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Previous month' })).toHaveFocus()
  })
})
