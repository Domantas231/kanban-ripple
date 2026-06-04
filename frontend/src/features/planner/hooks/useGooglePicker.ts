import { useState, useCallback, useRef, useEffect } from 'react'
import { getGooglePickerToken } from '@/features/planner/api/google'

export interface PickerFile {
  id: string
  name: string
  mimeType: string
}

type PickerCallback = (files: PickerFile[]) => void

const GOOGLE_API_SCRIPT = 'https://apis.google.com/js/api.js'

function loadScript(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const existing = document.querySelector(`script[src="${src}"]`)
    if (existing) {
      resolve()
      return
    }
    const script = document.createElement('script')
    script.src = src
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => reject(new Error(`Failed to load script: ${src}`))
    document.head.appendChild(script)
  })
}

function loadGapiPicker(): Promise<void> {
  return new Promise((resolve, reject) => {
    const gapi = (window as unknown as Record<string, unknown>).gapi as {
      load: (api: string, cb: { callback: () => void; onerror: (err: unknown) => void }) => void
    } | undefined

    if (!gapi) {
      reject(new Error('gapi not loaded'))
      return
    }

    gapi.load('picker', {
      callback: () => resolve(),
      onerror: (err: unknown) => reject(err),
    })
  })
}

export function useGooglePicker() {
  const [isLoading, setIsLoading] = useState(false)
  const scriptsLoadedRef = useRef(false)
  const pickerLoadedRef = useRef(false)
  const callbackRef = useRef<PickerCallback | null>(null)

  useEffect(() => {
    return () => {
      callbackRef.current = null
    }
  }, [])

  const ensureScriptsLoaded = useCallback(async () => {
    if (!scriptsLoadedRef.current) {
      await loadScript(GOOGLE_API_SCRIPT)
      scriptsLoadedRef.current = true
    }
    if (!pickerLoadedRef.current) {
      await loadGapiPicker()
      pickerLoadedRef.current = true
    }
  }, [])

  const openPicker = useCallback(
    (onSelect: PickerCallback) => {
      callbackRef.current = onSelect
      setIsLoading(true)

      Promise.all([ensureScriptsLoaded(), getGooglePickerToken()])
        .then(([, accessToken]) => {
          const google = (window as unknown as Record<string, unknown>).google as {
            picker: {
              PickerBuilder: new () => PickerBuilderInstance
              DocsView: new (viewId?: string) => DocsViewInstance
              Feature: { MULTISELECT_ENABLED: string }
              ViewId: { DOCS: string }
              Action: { PICKED: string; CANCEL: string }
            }
          }

          type DocsViewInstance = {
            setOwnedByMe: (ownedByMe: boolean) => DocsViewInstance
            setIncludeFolders: (include: boolean) => DocsViewInstance
          }

          type PickerBuilderInstance = {
            enableFeature: (feature: string) => PickerBuilderInstance
            addView: (view: string | DocsViewInstance) => PickerBuilderInstance
            setOAuthToken: (token: string) => PickerBuilderInstance
            setCallback: (cb: (data: PickerCallbackData) => void) => PickerBuilderInstance
            build: () => { setVisible: (visible: boolean) => void }
          }

          type PickerCallbackData = {
            action: string
            docs?: Array<{
              id: string
              name: string
              mimeType: string
            }>
          }

          const ownedDocsView = new google.picker.DocsView(google.picker.ViewId.DOCS)
            .setOwnedByMe(true)
            .setIncludeFolders(true)

          const picker = new google.picker.PickerBuilder()
            .enableFeature(google.picker.Feature.MULTISELECT_ENABLED)
            .addView(ownedDocsView)
            .setOAuthToken(accessToken)
            .setCallback((data: PickerCallbackData) => {
              if (data.action === google.picker.Action.PICKED && data.docs) {
                const files: PickerFile[] = data.docs.map((doc) => ({
                  id: doc.id,
                  name: doc.name,
                  mimeType: doc.mimeType,
                }))
                callbackRef.current?.(files)
              }
              if (
                data.action === google.picker.Action.PICKED ||
                data.action === google.picker.Action.CANCEL
              ) {
                setIsLoading(false)
              }
            })
            .build()

          picker.setVisible(true)
        })
        .catch(() => {
          setIsLoading(false)
        })
    },
    [ensureScriptsLoaded],
  )

  return { openPicker, isLoading }
}
