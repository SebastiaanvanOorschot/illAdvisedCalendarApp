import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GoogleLoginRequest, LogoutRequest, RefreshRequest } from '@/api/agenda-api-swagger'

const api = vi.hoisted(() => ({
    googleLogin: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn()
}))

vi.mock('@/api/agenda-api-swagger', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@/api/agenda-api-swagger')>()),
    AgendaAPI: vi.fn(function () {
        return api
    })
}))

const user = { id: 7, email: 'ada@example.test', name: 'Ada', profilePictureUrl: 'https://example.test/ada.png' }
const authResponse = (accessToken = 'new-access', refreshToken = 'new-refresh') => ({ accessToken, refreshToken, user })

/** An unsigned JWT; the frontend only decodes it */
const jwt = (payload: object) => {
    const encode = (part: object) => btoa(JSON.stringify(part)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
    return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`
}

/* useAuth keeps its state at module level and reads localStorage on import, so every test
   imports a fresh copy after setting up storage */
const freshUseAuth = async () => {
    vi.resetModules()
    const { useAuth } = await import('../useAuth')
    return useAuth()
}

beforeEach(() => {
    localStorage.clear()
})

describe('on startup', () => {
    it('restores the user from a stored access token', async () => {
        localStorage.setItem('accessToken', jwt({ sub: '7', email: 'ada@example.test', name: 'Ada', exp: 4102444800 }))
        localStorage.setItem('refreshToken', 'stored-refresh')

        const auth = await freshUseAuth()

        expect(auth.isAuthenticated.value).toBe(true)
        expect(auth.user.value).toEqual({ id: 7, email: 'ada@example.test', name: 'Ada' })
    })

    it('clears a stored token that cannot be decoded', async () => {
        localStorage.setItem('accessToken', 'not-a-jwt')
        localStorage.setItem('refreshToken', 'stored-refresh')

        const auth = await freshUseAuth()

        expect(auth.isAuthenticated.value).toBe(false)
        expect(localStorage.getItem('accessToken')).toBeNull()
        expect(localStorage.getItem('refreshToken')).toBeNull()
    })

    it('is logged out without a stored token', async () => {
        const auth = await freshUseAuth()

        expect(auth.isAuthenticated.value).toBe(false)
        expect(auth.user.value).toBeNull()
    })
})

describe('login', () => {
    it('sends the Google token and stores the tokens and user it gets back', async () => {
        api.googleLogin.mockResolvedValue(authResponse())
        const auth = await freshUseAuth()

        await auth.login('google-id-token')

        const request = api.googleLogin.mock.calls[0][0]
        expect(request).toBeInstanceOf(GoogleLoginRequest)
        expect(request.googleIdToken).toBe('google-id-token')
        expect(localStorage.getItem('accessToken')).toBe('new-access')
        expect(localStorage.getItem('refreshToken')).toBe('new-refresh')
        expect(auth.user.value).toEqual(user)
        expect(auth.isAuthenticated.value).toBe(true)
        expect(auth.isLoading.value).toBe(false)
        expect(auth.getAccessToken()).toBe('new-access')
    })

    it('rejects a response without tokens and stores nothing', async () => {
        api.googleLogin.mockResolvedValue({ user })
        const auth = await freshUseAuth()

        await expect(auth.login('google-id-token')).rejects.toThrow('missing tokens')

        expect(localStorage.getItem('accessToken')).toBeNull()
        expect(auth.isAuthenticated.value).toBe(false)
        expect(auth.error.value).toBe('Auth response is missing tokens')
    })

    it('rejects a response without user data', async () => {
        api.googleLogin.mockResolvedValue({ accessToken: 'a', refreshToken: 'r', user: { id: 7 } })
        const auth = await freshUseAuth()

        await expect(auth.login('google-id-token')).rejects.toThrow('missing user data')
        expect(localStorage.getItem('accessToken')).toBeNull()
    })

    it('keeps the error message and rethrows when the API fails', async () => {
        api.googleLogin.mockRejectedValue(new Error('Invalid Google token'))
        const auth = await freshUseAuth()

        await expect(auth.login('google-id-token')).rejects.toThrow('Invalid Google token')

        expect(auth.error.value).toBe('Invalid Google token')
        expect(auth.isLoading.value).toBe(false)
    })
})

describe('refreshAccessToken', () => {
    it('does nothing without a refresh token', async () => {
        const auth = await freshUseAuth()

        await expect(auth.refreshAccessToken()).resolves.toBe(false)
        expect(api.refresh).not.toHaveBeenCalled()
    })

    it('swaps in the new tokens', async () => {
        localStorage.setItem('refreshToken', 'stored-refresh')
        api.refresh.mockResolvedValue(authResponse('fresh-access', 'fresh-refresh'))
        const auth = await freshUseAuth()

        await expect(auth.refreshAccessToken()).resolves.toBe(true)

        const request = api.refresh.mock.calls[0][0]
        expect(request).toBeInstanceOf(RefreshRequest)
        expect(request.refreshToken).toBe('stored-refresh')
        expect(localStorage.getItem('accessToken')).toBe('fresh-access')
        expect(localStorage.getItem('refreshToken')).toBe('fresh-refresh')
    })

    it('logs out when the refresh is rejected', async () => {
        localStorage.setItem('accessToken', jwt({ sub: '7', email: 'ada@example.test', name: 'Ada', exp: 4102444800 }))
        localStorage.setItem('refreshToken', 'stored-refresh')
        api.refresh.mockRejectedValue(new Error('revoked'))
        api.logout.mockResolvedValue(undefined)
        const auth = await freshUseAuth()

        await expect(auth.refreshAccessToken()).resolves.toBe(false)

        await vi.waitFor(() => expect(auth.isAuthenticated.value).toBe(false))
        expect(localStorage.getItem('refreshToken')).toBeNull()
    })
})

describe('logout', () => {
    it('revokes the refresh token on the server and clears everything', async () => {
        api.googleLogin.mockResolvedValue(authResponse())
        api.logout.mockResolvedValue(undefined)
        const auth = await freshUseAuth()
        await auth.login('google-id-token')

        await auth.logout()

        const request = api.logout.mock.calls[0][0]
        expect(request).toBeInstanceOf(LogoutRequest)
        expect(request.refreshToken).toBe('new-refresh')
        expect(auth.user.value).toBeNull()
        expect(auth.isAuthenticated.value).toBe(false)
        expect(localStorage.getItem('accessToken')).toBeNull()
        expect(localStorage.getItem('refreshToken')).toBeNull()
    })

    it('still clears the local session when the server call fails', async () => {
        api.googleLogin.mockResolvedValue(authResponse())
        api.logout.mockRejectedValue(new Error('offline'))
        const auth = await freshUseAuth()
        await auth.login('google-id-token')

        await auth.logout()

        expect(auth.isAuthenticated.value).toBe(false)
        expect(localStorage.getItem('accessToken')).toBeNull()
    })
})
