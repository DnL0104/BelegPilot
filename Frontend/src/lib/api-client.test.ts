import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// api-client.test.ts — QA-02: shared refresh-promise dedupe
//
// The module has a module-level `refreshPromise` that gates concurrent 401s:
// only the first 401 fires /auth/refresh; others await the same promise.
//
// Strategy: vi.mock('axios') replaces axios.post (used by tryRefreshToken) and
// uses vi.resetModules() + dynamic import() per test so the module-level state
// (refreshPromise = null) is always fresh.

describe('api-client refresh-promise dedupe (QA-02)', () => {
  let refreshPostSpy: ReturnType<typeof vi.fn>

  beforeEach(() => {
    vi.resetModules()

    refreshPostSpy = vi.fn().mockResolvedValue({
      data: {
        accessToken: 'new-access',
        refreshToken: 'new-refresh',
        user: { id: '1', email: 'a@b.de', displayName: 'A' },
      },
    })

    // Provide refresh token in localStorage
    const localStorageMock = {
      getItem: (key: string) => (key === 'refreshToken' ? 'stored-rt' : null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
    }
    Object.defineProperty(globalThis, 'localStorage', {
      value: localStorageMock,
      writable: true,
      configurable: true,
    })
    Object.defineProperty(globalThis, 'window', {
      value: { location: { href: '' }, localStorage: localStorageMock },
      writable: true,
      configurable: true,
    })
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('calls /auth/refresh exactly once when N concurrent requests all receive 401', async () => {
    // Mock axios before importing the module-under-test
    vi.doMock('axios', async () => {
      const actual = await vi.importActual<typeof import('axios')>('axios')

      // Build a fake axios.create() that returns a controllable instance.
      // The instance adapter responds with 401 on first call, 200 on retry.
      let callsBeforeRefresh = 0

      const fakeCreate = () => {
        // Create a real instance so interceptors work
        const instance = actual.default.create({ baseURL: '/api/v1' })

        instance.defaults.adapter = async (config: import('axios').InternalAxiosRequestConfig) => {
          const cfg = config as typeof config & { _retry?: boolean }
          if (!cfg._retry) {
            callsBeforeRefresh++
            const err = Object.assign(new Error('401'), {
              response: { status: 401, data: {}, headers: {}, config, request: {} },
              config,
              isAxiosError: true,
            })
            return Promise.reject(err)
          }
          // Retry after refresh → success
          return { data: [], status: 200, statusText: 'OK', headers: {}, config }
        }

        return instance
      }

      const mockAxios = {
        ...actual.default,
        create: fakeCreate,
        post: refreshPostSpy,
      }

      return {
        default: mockAxios,
        // Named exports axios uses internally
        CanceledError: actual.CanceledError,
        AxiosError: actual.AxiosError,
      }
    })

    // Dynamically import after mocking so the module-level `api` uses the mock
    const { setAccessToken, getReceiptFiles } = await import('./api-client')

    setAccessToken('old-token')

    // Fire 3 concurrent requests that will all hit 401
    const N = 3
    const results = await Promise.allSettled(
      Array.from({ length: N }, () => getReceiptFiles())
    )

    // All retries should succeed (adapter returns 200 on _retry)
    const fulfilled = results.filter((r) => r.status === 'fulfilled')
    expect(fulfilled).toHaveLength(N)

    // The dedupe invariant: despite N concurrent 401s, /auth/refresh fires exactly once
    expect(refreshPostSpy).toHaveBeenCalledTimes(1)
    expect(refreshPostSpy).toHaveBeenCalledWith(
      '/api/v1/auth/refresh',
      { refreshToken: 'stored-rt' }
    )
  })
})
