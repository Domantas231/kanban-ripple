import { Link as RouterLink } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import SearchOffOutlinedIcon from '@mui/icons-material/SearchOffOutlined'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'

export function NotFoundPage() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '60vh', px: 2 }}>
      <Stack spacing={2.5} alignItems="center" textAlign="center" maxWidth={480}>
        <Box
          sx={{
            width: 80,
            height: 80,
            borderRadius: '50%',
            bgcolor: 'action.hover',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <SearchOffOutlinedIcon sx={{ fontSize: 40, color: 'text.disabled' }} />
        </Box>
        <Typography variant="h4" component="h1">
          Page Not Found
        </Typography>
        <Typography variant="body1" color="text.secondary">
          The page or resource you requested could not be found. It may have been moved or deleted.
        </Typography>
        <Button
          component={RouterLink}
          to="/projects"
          variant="contained"
          startIcon={<ArrowBackIcon />}
        >
          Back to Workspaces
        </Button>
      </Stack>
    </Box>
  )
}
