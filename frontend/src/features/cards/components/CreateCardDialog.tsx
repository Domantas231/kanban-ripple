import '@uiw/react-md-editor/markdown-editor.css'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  OutlinedInput,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { DatePicker } from '@mui/x-date-pickers/DatePicker'
import { useQueryClient } from '@tanstack/react-query'
import { parseISO, format as formatDateFns } from 'date-fns'
import MDEditor from '@uiw/react-md-editor'
import { useEffect, useMemo } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { useCreateCard } from '@/features/cards/api/cards'
import { boardsQueryKeys } from '@/features/boards'
import type { Guid, ProjectMember, Tag } from '@/lib/types'

const createCardSchema = z.object({
  title: z.string().trim().min(1, 'Title is required'),
  description: z.string().default(''),
  tagIds: z.array(z.string()).default([]),
  assigneeUserIds: z.array(z.string()).default([]),
  startDate: z.string().default(''),
  dueDate: z.string().default(''),
}).refine(
  (data) => {
    if (data.startDate && data.dueDate) {
      return data.dueDate >= data.startDate
    }
    return true
  },
  { message: 'Due date cannot be before start date', path: ['dueDate'] },
)

type CreateCardFormInput = z.input<typeof createCardSchema>
type CreateCardFormValues = z.output<typeof createCardSchema>

type CreateCardDialogProps = {
  open: boolean
  columnId: Guid | null
  boardId: Guid
  tags: Tag[]
  members: ProjectMember[]
  onClose: () => void
}

export function CreateCardDialog({ open, columnId, boardId, tags, members, onClose }: CreateCardDialogProps) {
  const queryClient = useQueryClient()
  const createCardMutation = useCreateCard()

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<CreateCardFormInput, unknown, CreateCardFormValues>({
    resolver: zodResolver(createCardSchema),
    defaultValues: {
      title: '',
      description: '',
      tagIds: [],
      assigneeUserIds: [],
      startDate: '',
      dueDate: '',
    },
  })

  useEffect(() => {
    if (open) {
      reset({
        title: '',
        description: '',
        tagIds: [],
        assigneeUserIds: [],
        startDate: '',
        dueDate: '',
      })
    }
  }, [open, reset])

  const description = watch('description')

  const sortedMembers = useMemo(
    () =>
      [...members].sort((a, b) => {
        const left = a.user?.userName?.trim() || a.email?.trim() || a.user?.email?.trim() || a.userId
        const right = b.user?.userName?.trim() || b.email?.trim() || b.user?.email?.trim() || b.userId
        return left.localeCompare(right)
      }),
    [members],
  )

  const handleDialogClose = () => {
    if (isSubmitting || createCardMutation.isPending) {
      return
    }

    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    if (!columnId) {
      return
    }

    await createCardMutation.mutateAsync({
      columnId,
      data: {
        title: values.title.trim(),
        description: values.description.trim().length > 0 ? values.description : null,
        startDate: values.startDate.trim().length > 0 ? new Date(values.startDate).toISOString() : null,
        dueDate: values.dueDate.trim().length > 0 ? new Date(values.dueDate).toISOString() : null,
        tagIds: values.tagIds,
        assigneeUserIds: values.assigneeUserIds,
      },
    })

    await queryClient.invalidateQueries({ queryKey: boardsQueryKeys.boardCards(boardId) })
    onClose()
  })

  return (
    <Dialog open={open} onClose={handleDialogClose} fullWidth maxWidth="md">
      <DialogTitle>Create Task</DialogTitle>
      <DialogContent>
        <Stack component="form" spacing={2} sx={{ mt: 1 }} onSubmit={onSubmit} noValidate>
          <TextField
            label="Title"
            autoFocus
            fullWidth
            error={Boolean(errors.title)}
            helperText={errors.title?.message}
            {...register('title')}
          />

          <Controller
            control={control}
            name="description"
            render={({ field }) => (
              <Stack spacing={1}>
                <Typography variant="body2" color="text.secondary">
                  Description
                </Typography>
                <Box data-color-mode="light">
                  <MDEditor value={field.value} onChange={(value) => field.onChange(value ?? '')} height={220} preview="edit" />
                </Box>
                <Typography variant="body2" color="text.secondary">
                  Preview
                </Typography>
                <Box
                  data-color-mode="light"
                  sx={{
                    border: 1,
                    borderColor: 'divider',
                    borderRadius: 1,
                    p: 1.5,
                    minHeight: 120,
                  }}
                >
                  <MDEditor.Markdown source={description?.trim().length ? description : '_Nothing to preview._'} />
                </Box>
              </Stack>
            )}
          />

          <Controller
            control={control}
            name="tagIds"
            render={({ field }) => (
              <FormControl fullWidth>
                <InputLabel id="create-card-tags-label">Tags</InputLabel>
                <Select
                  labelId="create-card-tags-label"
                  multiple
                  value={field.value}
                  onChange={(event) => field.onChange(event.target.value as Guid[])}
                  input={<OutlinedInput label="Tags" />}
                  renderValue={(selected) => {
                    const selectedValues = selected as Guid[]
                    if (selectedValues.length === 0) {
                      return 'None'
                    }

                    return (
                      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                        {selectedValues.map((tagId) => {
                          const tag = tags.find((item) => item.id === tagId)
                          return <Chip key={tagId} size="small" label={tag?.name ?? tagId} />
                        })}
                      </Box>
                    )
                  }}
                >
                  {tags.map((tag) => (
                    <MenuItem key={tag.id} value={tag.id}>
                      {tag.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            )}
          />

          <Controller
            control={control}
            name="assigneeUserIds"
            render={({ field }) => (
              <FormControl fullWidth>
                <InputLabel id="create-card-assignees-label">Assigned users</InputLabel>
                <Select
                  labelId="create-card-assignees-label"
                  multiple
                  value={field.value}
                  onChange={(event) => field.onChange(event.target.value as Guid[])}
                  input={<OutlinedInput label="Assigned users" />}
                  renderValue={(selected) => {
                    const selectedValues = selected as Guid[]
                    if (selectedValues.length === 0) {
                      return 'None'
                    }

                    return (
                      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
                        {selectedValues.map((userId) => {
                          const member = sortedMembers.find((item) => item.userId === userId)
                          const label =
                            member?.user?.userName?.trim() || member?.email?.trim() || member?.user?.email?.trim() || userId
                          return <Chip key={userId} size="small" label={label} />
                        })}
                      </Box>
                    )
                  }}
                >
                  {sortedMembers.map((member) => {
                    const label =
                      member.userName?.trim() || member.user?.userName?.trim() || member.email?.trim() || member.user?.email?.trim() || member.userId
                    return (
                      <MenuItem key={member.userId} value={member.userId}>
                        {label}
                      </MenuItem>
                    )
                  })}
                </Select>
              </FormControl>
            )}
          />

          <Stack direction="row" spacing={2}>
            <Controller
              name="startDate"
              control={control}
              render={({ field }) => (
                <DatePicker
                  label="Start date"
                  value={field.value ? parseISO(field.value) : null}
                  onChange={(val) => field.onChange(val ? formatDateFns(val, 'yyyy-MM-dd') : '')}
                  slotProps={{
                    textField: { fullWidth: true, size: 'small' },
                    actionBar: { actions: ['clear', 'accept'] },
                  }}
                />
              )}
            />
            <Controller
              name="dueDate"
              control={control}
              render={({ field }) => (
                <DatePicker
                  label="Due date"
                  value={field.value ? parseISO(field.value) : null}
                  onChange={(val) => field.onChange(val ? formatDateFns(val, 'yyyy-MM-dd') : '')}
                  slotProps={{
                    textField: {
                      fullWidth: true,
                      size: 'small',
                      error: Boolean(errors.dueDate),
                      helperText: errors.dueDate?.message,
                    },
                    actionBar: { actions: ['clear', 'accept'] },
                  }}
                />
              )}
            />
          </Stack>

          {createCardMutation.isError ? <Alert severity="error">Unable to create task. Please try again.</Alert> : null}

          <DialogActions sx={{ px: 0 }}>
            <Button onClick={handleDialogClose} disabled={isSubmitting || createCardMutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={isSubmitting || createCardMutation.isPending || !columnId}>
              {isSubmitting || createCardMutation.isPending ? 'Creating...' : 'Create'}
            </Button>
          </DialogActions>
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
