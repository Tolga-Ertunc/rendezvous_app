const accessTokenKey = "rendezvous.accessToken"
const refreshTokenKey = "rendezvous.refreshToken"
const authStorageChangedEvent = "rendezvous.auth-storage-changed"

function canUseStorage() {
  return typeof window !== "undefined" && typeof window.sessionStorage !== "undefined"
}

export function getAccessToken() {
  if (!canUseStorage()) {
    return null
  }

  return window.sessionStorage.getItem(accessTokenKey)
}

export function getRefreshToken() {
  if (!canUseStorage()) {
    return null
  }

  return window.sessionStorage.getItem(refreshTokenKey)
}

export function setAuthTokens(accessToken: string, refreshToken: string) {
  if (!canUseStorage()) {
    return
  }

  window.sessionStorage.setItem(accessTokenKey, accessToken)
  window.sessionStorage.setItem(refreshTokenKey, refreshToken)
  window.dispatchEvent(new Event(authStorageChangedEvent))
}

export function clearAuthTokens() {
  if (!canUseStorage()) {
    return
  }

  window.sessionStorage.removeItem(accessTokenKey)
  window.sessionStorage.removeItem(refreshTokenKey)
  window.dispatchEvent(new Event(authStorageChangedEvent))
}

export function subscribeToAuthTokenChanges(onStoreChange: () => void) {
  window.addEventListener("storage", onStoreChange)
  window.addEventListener(authStorageChangedEvent, onStoreChange)

  return () => {
    window.removeEventListener("storage", onStoreChange)
    window.removeEventListener(authStorageChangedEvent, onStoreChange)
  }
}
