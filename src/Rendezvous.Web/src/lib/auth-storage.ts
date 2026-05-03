const accessTokenKey = "rendezvous.accessToken"
const refreshTokenKey = "rendezvous.refreshToken"

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
}

export function clearAuthTokens() {
  if (!canUseStorage()) {
    return
  }

  window.sessionStorage.removeItem(accessTokenKey)
  window.sessionStorage.removeItem(refreshTokenKey)
}
