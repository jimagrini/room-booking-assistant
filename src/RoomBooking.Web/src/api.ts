export interface AuthenticatedUser {
  id: string
  username: string
}

export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
  user: AuthenticatedUser
}

export interface AssistantMessageResponse {
  conversationId: string
  message: string
  toolsUsed: string[]
}

interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, message: string, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

async function post<T>(
  path: string,
  body: unknown,
  accessToken?: string,
): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(accessToken
          ? { Authorization: `Bearer ${accessToken}` }
          : {}),
      },
      body: JSON.stringify(body),
    })
  } catch {
    throw new ApiError(
      0,
      'No se pudo conectar con la API. Verificá que el backend esté ejecutándose.',
    )
  }

  if (!response.ok) {
    let problem: ProblemDetails | undefined

    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem = undefined
    }

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? 'La solicitud no pudo completarse.',
      problem?.code,
    )
  }

  return (await response.json()) as T
}

export function login(
  username: string,
  password: string,
): Promise<LoginResponse> {
  return post<LoginResponse>('/api/auth/login', { username, password })
}

export function sendAssistantMessage(
  accessToken: string,
  message: string,
  conversationId?: string,
): Promise<AssistantMessageResponse> {
  return post<AssistantMessageResponse>(
    '/api/assistant/messages',
    {
      message,
      ...(conversationId ? { conversationId } : {}),
    },
    accessToken,
  )
}
