export class ApiError extends Error {
  constructor(readonly status: number, readonly code: string, title: string) {
    super(title)
  }
}

let currentToken: () => string | null = () => null
let refreshToken: () => Promise<string | null> = async () => null

export function useSessionInApi(access: typeof currentToken, refresh: typeof refreshToken) {
  currentToken = access
  refreshToken = refresh
}

type ApiInput = { method?: string; body?: unknown; signal?: AbortSignal; auth?: boolean; retry?: boolean }

export async function apiResponse(path: string, options: ApiInput = {}): Promise<Response> {
  const headers = new Headers({ Accept: 'application/json' })
  const token = options.auth === false ? null : currentToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const form = options.body instanceof FormData
  if (!form && options.body !== undefined) headers.set('Content-Type', 'application/json')
  const response = await fetch(`/api/v1${path}`, {
    method: options.method ?? 'GET',
    credentials: 'same-origin',
    headers,
    body: form ? (options.body as FormData) : options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })
  if (response.status === 401 && options.auth !== false && options.retry !== false && await refreshToken()) {
    return apiResponse(path, { ...options, retry: false })
  }
  if (response.ok) return response
  const problem = await response.json().catch(() => ({})) as { title?: string; code?: string; errors?: Record<string, string[]> }
  const validation = Object.values(problem.errors ?? {}).flat().find(Boolean)
  const fallback = response.status === 401 ? 'Сессия завершилась. Войдите снова.' : 'Запрос не выполнен. Попробуйте ещё раз.'
  throw new ApiError(response.status, problem.code ?? '', validation ?? problem.title ?? fallback)
}

export async function api<T>(path: string, options?: ApiInput): Promise<T> {
  const response = await apiResponse(path, options)
  return response.status === 204 ? undefined as T : await response.json() as T
}

export function explain(error: unknown) {
  return error instanceof ApiError ? error.message : 'Соединение прервалось. Проверьте сеть и повторите действие.'
}
