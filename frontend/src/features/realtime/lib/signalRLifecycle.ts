import { useAuthStore } from '@/features/auth'
import { signalRService } from './signalRService'

let initialized = false

function synchronizeConnection(isAuthenticated: boolean): void {
  if (isAuthenticated) {
    void signalRService.connect()
    return
  }

  void signalRService.disconnect()
}

export function initializeSignalRConnectionLifecycle(): void {
  if (initialized) {
    return
  }

  initialized = true

  const authState = useAuthStore.getState()
  synchronizeConnection(Boolean(authState.isAuthenticated && authState.accessToken))

  useAuthStore.subscribe((state, previousState) => {
    const wasAuthenticated = Boolean(previousState.isAuthenticated && previousState.accessToken)
    const isAuthenticated = Boolean(state.isAuthenticated && state.accessToken)

    if (isAuthenticated === wasAuthenticated) {
      return
    }

    synchronizeConnection(isAuthenticated)
  })
}
