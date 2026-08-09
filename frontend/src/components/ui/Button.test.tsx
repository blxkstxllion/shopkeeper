import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Button } from './Button'

describe('Button', () => {
  it('renders its label and responds to clicks', async () => {
    const onClick = vi.fn()
    render(<Button onClick={onClick}>Create sale</Button>)

    const button = screen.getByRole('button', { name: 'Create sale' })
    await userEvent.click(button)

    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('disables itself and shows a spinner while loading, ignoring clicks', async () => {
    const onClick = vi.fn()
    render(
      <Button onClick={onClick} isLoading>
        Save
      </Button>,
    )

    const button = screen.getByRole('button', { name: 'Save' })
    expect(button).toBeDisabled()

    await userEvent.click(button)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('stays disabled when explicitly disabled, independent of isLoading', () => {
    render(<Button disabled>Unavailable</Button>)
    expect(screen.getByRole('button', { name: 'Unavailable' })).toBeDisabled()
  })
})
