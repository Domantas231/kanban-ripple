import { create } from 'zustand'

export type RealtimeConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'

type RealtimeState = {
  connectionState: RealtimeConnectionState
  setConnectionState: (state: RealtimeConnectionState) => void
}

export const useRealtimeStore = create<RealtimeState>((set) => ({
  connectionState: 'disconnected',
  setConnectionState: (state) => set({ connectionState: state }),
}))
