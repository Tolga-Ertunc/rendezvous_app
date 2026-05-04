import {
  clearAuthTokens,
  getAccessToken,
  getRefreshToken,
} from "@/lib/auth-storage"

const apiBasePath = "/backend-api"

type ApiRequestOptions = RequestInit & {
  ignoreNoContent?: boolean
  skipAuthRefresh?: boolean
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body?: unknown
  ) {
    super(message)
  }
}

export async function apiRequest<T>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<T> {
  return sendRequest<T>(path, options, true)
}

async function sendRequest<T>(
  path: string,
  options: ApiRequestOptions,
  allowRefresh: boolean
): Promise<T> {
  const response = await fetch(`${apiBasePath}${path}`, {
    ...options,
    headers: createHeaders(options.headers),
  })

  if (
    response.status === 401 &&
    allowRefresh &&
    !options.skipAuthRefresh &&
    (await refreshAccessToken())
  ) {
    return sendRequest<T>(path, options, false)
  }

  if (!response.ok) {
    throw new ApiError("Request failed", response.status, await readErrorBody(response))
  }

  if (response.status === 204 || options.ignoreNoContent) {
    return undefined as T
  }

  return (await response.json()) as T
}

async function readErrorBody(response: Response) {
  const contentType = response.headers.get("content-type") ?? ""
  if (!contentType.includes("application/json")) {
    return undefined
  }

  try {
    return await response.json()
  } catch {
    return undefined
  }
}

function createHeaders(headers: HeadersInit | undefined) {
  const nextHeaders = new Headers(headers)

  if (!nextHeaders.has("content-type")) {
    nextHeaders.set("content-type", "application/json")
  }

  const accessToken = getAccessToken()
  if (accessToken && !nextHeaders.has("authorization")) {
    nextHeaders.set("authorization", `Bearer ${accessToken}`)
  }

  return nextHeaders
}

async function refreshAccessToken() {
  const refreshToken = getRefreshToken()

  if (!refreshToken) {
    clearAuthTokens()
    return false
  }

  try {
    const { refreshSession } = await import("@/lib/auth-api")
    await refreshSession(refreshToken)

    return true
  } catch {
    clearAuthTokens()

    return false
  }
}
