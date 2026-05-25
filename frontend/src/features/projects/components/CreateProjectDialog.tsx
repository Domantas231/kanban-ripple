import { zodResolver } from '@hookform/resolvers/zod'
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useCreateProject } from '@/features/projects/api/projects'

const createProjectSchema = z.object({
  name: z.string().trim().min(1, 'Workspace name is required'),
})

type CreateProjectFormValues = z.infer<typeof createProjectSchema>

type CreateProjectDialogProps = {
  open: boolean
  onClose: () => void
  onCreated?: () => void
}

export function CreateProjectDialog({ open, onClose, onCreated }: CreateProjectDialogProps) {
  const createProjectMutation = useCreateProject()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateProjectFormValues>({
    resolver: zodResolver(createProjectSchema),
    defaultValues: {
      name: '',
    },
  })

  const handleDialogClose = () => {
    if (isSubmitting || createProjectMutation.isPending) {
      return
    }

    reset()
    onClose()
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      await createProjectMutation.mutateAsync({
        name: values.name.trim(),
      })
    } catch {
      // The Alert below renders via createProjectMutation.isError; swallowing
      // here prevents react-hook-form from leaking an unhandled rejection.
      return
    }

    reset()
    onClose()
    onCreated?.()
  })

  return (
    <Dialog open={open} onClose={handleDialogClose} fullWidth maxWidth="xs">
      <DialogTitle>Create Workspace</DialogTitle>
      <DialogContent>
        <Stack component="form" spacing={2} sx={{ mt: 1 }} onSubmit={onSubmit} noValidate>
          <TextField
            label="Workspace name"
            fullWidth
            error={Boolean(errors.name)}
            helperText={errors.name?.message}
            {...register('name')}
            inputRef={(input: HTMLInputElement | null) => {
              register('name').ref(input)
              if (input) {
                requestAnimationFrame(() => input.focus())
              }
            }}
          />

          {createProjectMutation.isError ? (
            <Alert severity="error">Unable to create workspace. Please try again.</Alert>
          ) : null}

          <DialogActions sx={{ px: 0 }}>
            <Button onClick={handleDialogClose} disabled={isSubmitting || createProjectMutation.isPending}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" disabled={isSubmitting || createProjectMutation.isPending}>
              {isSubmitting || createProjectMutation.isPending ? 'Creating...' : 'Create'}
            </Button>
          </DialogActions>
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
