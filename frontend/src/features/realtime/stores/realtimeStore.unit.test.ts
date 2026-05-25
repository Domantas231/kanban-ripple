import { afterEach, describe, expect, it } from 'vitest'
import { useRealtimeStore } from './realtimeStore'

afterEach(() => {
  // Reset to the documented default so tests stay independent.
  useRealtimeStore.getState().setConnectionState('disconnected')
})

describe('realtimeStore', () => {
  it('starts disconnected', () => {
    expect(useRealtimeStore.getState().connectionState).toBe('disconnected')
  })

  it('moves through the documented transitions', () => {
    const setState = useRealtimeStore.getState().setConnectionState

    setState('connecting')
    expect(useRealtimeStore.getState().connectionState).toBe('connecting')

    setState('connected')
    expect(useRealtimeStore.getState().connectionState).toBe('connected')

    setState('reconnecting')
    expect(useRealtimeStore.getState().connectionState).toBe('reconnecting')

    setState('disconnected')
    expect(useRealtimeStore.getState().connectionState).toBe('disconnected')
  })
})
