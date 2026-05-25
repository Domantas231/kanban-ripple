import Chip from '@mui/material/Chip'
import Tooltip from '@mui/material/Tooltip'
import { alpha, useTheme } from '@mui/material/styles'
import type { Tag } from '@/lib/types'
import { useUiStore } from '@/stores/uiStore'

type TagChipProps = {
  tag: Pick<Tag, 'name' | 'color'>
  tagId?: string
  size?: 'small' | 'medium'
  onDelete?: () => void
}

function isValidHexColor(value: string): boolean {
  return /^#([0-9a-fA-F]{6})$/.test(value.trim())
}

function normalizeTagColor(color: string | null | undefined): string {
  const trimmed = color?.trim() ?? ''
  if (isValidHexColor(trimmed)) {
    return trimmed
  }

  return '#9e9e9e'
}

function hexToRgb(hex: string): { r: number; g: number; b: number } {
  const val = parseInt(hex.slice(1), 16)
  return { r: (val >> 16) & 255, g: (val >> 8) & 255, b: val & 255 }
}

/** Relative luminance per WCAG 2.1 */
function relativeLuminance(r: number, g: number, b: number): number {
  const [rs, gs, bs] = [r, g, b].map((c) => {
    const s = c / 255
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4)
  })
  return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs
}

function contrastRatio(hex1: string, hex2: string): number {
  const c1 = hexToRgb(hex1)
  const c2 = hexToRgb(hex2)
  const l1 = relativeLuminance(c1.r, c1.g, c1.b)
  const l2 = relativeLuminance(c2.r, c2.g, c2.b)
  const lighter = Math.max(l1, l2)
  const darker = Math.min(l1, l2)
  return (lighter + 0.05) / (darker + 0.05)
}

/**
 * Darken or lighten a hex color to meet 4.5:1 contrast against a background.
 * Returns the original color if it already passes.
 */
function ensureContrast(fgHex: string, bgHex: string, minRatio = 4.5): string {
  if (contrastRatio(fgHex, bgHex) >= minRatio) return fgHex

  const bgLum = relativeLuminance(...Object.values(hexToRgb(bgHex)) as [number, number, number])
  const fg = hexToRgb(fgHex)
  const shouldDarken = bgLum > 0.5

  // Iteratively adjust brightness
  let { r, g, b } = fg
  for (let i = 0; i < 20; i++) {
    const factor = shouldDarken ? 0.85 : 1.2
    r = Math.min(255, Math.max(0, Math.round(r * factor)))
    g = Math.min(255, Math.max(0, Math.round(g * factor)))
    b = Math.min(255, Math.max(0, Math.round(b * factor)))

    const candidate = `#${((1 << 24) | (r << 16) | (g << 8) | b).toString(16).slice(1)}`
    if (contrastRatio(candidate, bgHex) >= minRatio) return candidate
  }

  // Fallback: use black or white
  return shouldDarken ? '#1e293b' : '#f1f5f9'
}

export function TagChip({ tag, tagId, size = 'small', onDelete }: TagChipProps) {
  const theme = useTheme()
  const displayMode = useUiStore((state) => (tagId ? state.tagDisplayModes[tagId] : undefined) ?? 'both')
  const tagColor = normalizeTagColor(tag.color)
  const bgHex = theme.palette.mode === 'dark' ? '#151921' : '#FFFFFF'

  const compact = size === 'small'

  const compactSx = compact
    ? {
        height: 20,
        fontSize: '0.6875rem',
        maxWidth: '100%',
        '& .MuiChip-label': {
          px: 1,
          textOverflow: 'ellipsis',
          overflow: 'hidden',
          whiteSpace: 'nowrap',
        },
      }
    : { maxWidth: '100%' }

  if (displayMode === 'color') {
    return (
      <Tooltip title={tag.name} arrow>
        <Chip
          size={size}
          label=""
          aria-label={tag.name}
          onDelete={onDelete}
          sx={{
            ...compactSx,
            bgcolor: alpha(tagColor, 0.40),
            '& .MuiChip-label': { px: compact ? 1.5 : 2.5 },
            '& .MuiChip-deleteIcon': onDelete ? {
              color: alpha(theme.palette.text.primary, 0.5),
              ml: 0,
              '&:hover': {
                color: theme.palette.text.primary,
              },
            } : undefined,
          }}
        />
      </Tooltip>
    )
  }

  if (displayMode === 'name') {
    return (
      <Chip
        size={size}
        label={tag.name}
        onDelete={onDelete}
        variant="outlined"
        sx={{
          ...compactSx,
          '& .MuiChip-deleteIcon': onDelete ? {
            color: alpha(theme.palette.text.primary, 0.5),
            '&:hover': {
              color: theme.palette.text.primary,
            },
          } : undefined,
        }}
      />
    )
  }

  // "both" mode: colored background + name with guaranteed contrast
  const accessibleColor = ensureContrast(tagColor, bgHex)

  return (
    <Chip
      size={size}
      label={tag.name}
      onDelete={onDelete}
      sx={{
        ...compactSx,
        bgcolor: alpha(tagColor, 0.12),
        color: accessibleColor,
        fontWeight: 600,
        border: '1px solid',
        borderColor: alpha(tagColor, 0.2),
        '& .MuiChip-deleteIcon': onDelete ? {
          color: alpha(accessibleColor, 0.7),
          '&:hover': {
            color: accessibleColor,
          },
        } : undefined,
      }}
    />
  )
}
