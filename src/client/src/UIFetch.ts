import { type Ref } from 'vue'

type FetchResult =
  { succeeded: true, response: Response } |
  { succeeded: false, response?: Response}

export const uiFetch = async (isLoadingRef: Ref<boolean>, hasFailedRef: Ref<boolean>, fetchUrl: string, fetchParams?: RequestInit) : Promise<FetchResult> => {
  isLoadingRef.value = true
  hasFailedRef.value = false

  try {
    const response = await fetch(fetchUrl, fetchParams)
    if (!response.ok) {
      throw response
    }
    return { succeeded: true, response }
  }
  catch (e) {
    hasFailedRef.value = true
    return { succeeded: false, response: e instanceof Response ? e : undefined }
  }
  finally {
    isLoadingRef.value = false
  }
}
