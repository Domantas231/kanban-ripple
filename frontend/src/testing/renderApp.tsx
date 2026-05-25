import { render } from '@testing-library/react'
import { CssBaseline, ThemeProvider } from '@mui/material'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryHistory, createRouter } from '@tanstack/react-router'
import { routeTree } from '../app/routeTree.gen'
import { appTheme } from '../app/theme'

type RenderAppInput = string | { route: string }

export async function renderApp(input: RenderAppInput) {
  const initialEntry = typeof input === 'string' ? input : input.route

  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
  })

  const history = createMemoryHistory({
    initialEntries: [initialEntry],
  })

  const router = createRouter({
    routeTree,
    history,
  })

  const rendered = render(
    <ThemeProvider theme={appTheme}>
      <CssBaseline />
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  )

  await router.load()

  return {
    ...rendered,
    router,
    queryClient,
  }
}
