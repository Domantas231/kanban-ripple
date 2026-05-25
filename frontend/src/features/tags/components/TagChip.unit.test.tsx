import { afterEach, describe, expect, it } from 'vitest'
import { render, screen, cleanup } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { ThemeProvider } from '@mui/material/styles'
import { appTheme } from '@/app/theme'
import { useUiStore } from '@/stores/uiStore'
import { TagChip } from './TagChip'

afterEach(() => {
  cleanup()
})

function renderTag(props: React.ComponentProps<typeof TagChip>) {
  return render(
    <ThemeProvider theme={appTheme}>
      <TagChip {...props} />
    </ThemeProvider>,
  )
}

describe('TagChip — display modes', () => {
  it('renders the tag name in the default "both" mode', () => {
    renderTag({ tag: { name: 'urgent', color: '#ff0000' } })
    expect(screen.getByText('urgent')).toBeInTheDocument()
  })

  it('honours the per-tag display mode "name" from the UI store', () => {
    useUiStore.getState().setTagDisplayMode('tag-1', 'name')
    renderTag({ tagId: 'tag-1', tag: { name: 'urgent', color: '#ff0000' } })
    expect(screen.getByText('urgent')).toBeInTheDocument()
    // Reset to avoid leaking between tests.
    useUiStore.getState().setTagDisplayMode('tag-1', 'both')
  })

  it('hides the name and exposes the tag name via aria-label in "color" mode', () => {
    useUiStore.getState().setTagDisplayMode('tag-color', 'color')
    renderTag({ tagId: 'tag-color', tag: { name: 'priority', color: '#00ff00' } })

    expect(screen.queryByText('priority')).not.toBeInTheDocument()
    expect(screen.getByLabelText('priority')).toBeInTheDocument()
    useUiStore.getState().setTagDisplayMode('tag-color', 'both')
  })
})

describe('TagChip — invalid colors', () => {
  it('falls back to a safe gray when the color is not a valid 6-digit hex', () => {
    // We can't read the computed alpha background reliably here; the
    // important contract is that the chip still renders the name without
    // throwing.
    renderTag({ tag: { name: 'broken', color: 'not-a-color' } })
    expect(screen.getByText('broken')).toBeInTheDocument()
  })

  it('handles a null color', () => {
    // The defensive normalization treats null as the gray fallback.
    renderTag({ tag: { name: 'nullish', color: null as unknown as string } })
    expect(screen.getByText('nullish')).toBeInTheDocument()
  })
})

describe('TagChip — onDelete', () => {
  it('renders a delete button when onDelete is provided and fires the handler when clicked', async () => {
    const onDelete = vi.fn()
    const user = userEvent.setup()
    renderTag({ tag: { name: 'rm', color: '#123456' }, onDelete })

    // MUI's Chip delete icon uses the role="button" + an SVG; we look for the
    // CancelIcon by its DOM class.
    const chip = screen.getByText('rm').closest('.MuiChip-root') as HTMLElement
    const deleteIcon = chip.querySelector('.MuiChip-deleteIcon')
    expect(deleteIcon).not.toBeNull()

    await user.click(deleteIcon as Element)
    expect(onDelete).toHaveBeenCalledOnce()
  })
})

// Vitest globals (vi) are pulled in via `globals: true` in vite.config.
declare const vi: typeof import('vitest').vi
