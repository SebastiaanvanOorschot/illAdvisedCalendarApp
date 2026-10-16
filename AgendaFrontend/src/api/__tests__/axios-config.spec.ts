import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { createAuthenticatedAxios } from '../axios-config'

const API = 'https://api.test'

type Respond = (config: InternalAxiosRequestConfig) => { status: number; data?: unknown }

/** Seen requests: the URL plus the Authorization header each attempt was sent with */
let seen: { url?: string; auth?: string }[]
let client: AxiosInstance
let location: { href: string }

/* the transport: answers like a server would, rejecting non-2xx the way axios' own adapters do */
const answerWith = (respond: Respond) => {
    client.defaults.adapter = async (config) => {
        // yield to the event loop like real I/O, so a refresh loop runs into the test timeout instead of hanging the run
        await new Promise((resolve) => setTimeout(resolve, 0))
        seen.push({ url: config.url, auth: config.headers?.Authorization as string | undefined })
        const { status, data = {} } = respond(config)
        const response = { status, data, statusText: '', headers: {}, config }
        if (status >= 400) {
            throw new AxiosError(`Request failed with status code ${status}`, 'ERR_BAD_REQUEST', config, null, response)
        }
        return response
    }
}

/* the API: 401 for the old token, 200 for anything else */
const tokenCheckingServer: Respond = (config) =>
    config.headers?.Authorization === 'Bearer new-access' ? { status: 200, data: { ok: config.url } } : { status: 401 }

const refreshSucceeds = () =>
    vi.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: 'new-access', refreshToken: 'new-refresh' } })

beforeEach(() => {
    vi.stubEnv('VITE_API_URL', API)
    localStorage.clear()
    seen = []
    // the interceptor redirects with window.location.href; jsdom cannot navigate, so stand in for it
    location = { href: '' }
    vi.stubGlobal('window', { location })
    client = createAuthenticatedAxios()
})

afterEach(() => {
    localStorage.clear()
})

describe('authenticated axios', () => {
    it('uses the API url from the environment', () => {
        expect(client.defaults.baseURL).toBe(API)
    })

    it('sends the stored access token as a Bearer header', async () => {
        localStorage.setItem('accessToken', 'stored-access')
        answerWith(() => ({ status: 200 }))

        await client.get('/api/Events')

        expect(seen).toEqual([{ url: '/api/Events', auth: 'Bearer stored-access' }])
    })

    it('sends no Authorization header without a token', async () => {
        answerWith(() => ({ status: 200 }))

        await client.get('/api/Events')

        expect(seen[0].auth).toBeUndefined()
    })

    it('passes errors other than 401 through without refreshing', async () => {
        localStorage.setItem('refreshToken', 'stored-refresh')
        const post = vi.spyOn(axios, 'post')
        answerWith(() => ({ status: 500 }))

        await expect(client.get('/api/Events')).rejects.toMatchObject({ response: { status: 500 } })
        expect(post).not.toHaveBeenCalled()
    })
})

describe('on a 401', () => {
    beforeEach(() => {
        localStorage.setItem('accessToken', 'old-access')
        localStorage.setItem('refreshToken', 'stored-refresh')
    })

    it('refreshes the tokens, stores them and retries the request with the new token', async () => {
        const post = refreshSucceeds()
        answerWith(tokenCheckingServer)

        const response = await client.get('/api/Events')

        expect(post).toHaveBeenCalledWith(`${API}/api/Auth/refresh`, { refreshToken: 'stored-refresh' })
        expect(localStorage.getItem('accessToken')).toBe('new-access')
        expect(localStorage.getItem('refreshToken')).toBe('new-refresh')
        expect(seen.map((s) => s.auth)).toEqual(['Bearer old-access', 'Bearer new-access'])
        expect(response.data).toEqual({ ok: '/api/Events' })
    })

    it('refreshes only once for several requests that fail at the same time, then retries them all', async () => {
        let finishRefresh!: () => void
        const post = vi.spyOn(axios, 'post').mockImplementation(
            () => new Promise((resolve) => {
                finishRefresh = () => resolve({ data: { accessToken: 'new-access', refreshToken: 'new-refresh' } })
            })
        )
        answerWith(tokenCheckingServer)

        const requests = ['/api/Events', '/api/Weather', '/api/MonthImages'].map((url) => client.get(url))
        await vi.waitFor(() => expect(seen).toHaveLength(3))
        finishRefresh()
        const responses = await Promise.all(requests)

        expect(post).toHaveBeenCalledTimes(1)
        expect(responses.map((r) => r.data.ok)).toEqual(['/api/Events', '/api/Weather', '/api/MonthImages'])
        expect(seen.filter((s) => s.auth === 'Bearer new-access')).toHaveLength(3)
    })

    it('does not refresh again when the retried request is still unauthorized', async () => {
        const post = refreshSucceeds()
        answerWith(() => ({ status: 401 }))

        await expect(client.get('/api/Events')).rejects.toMatchObject({ response: { status: 401 } })

        expect(post).toHaveBeenCalledTimes(1)
        expect(seen).toHaveLength(2)
    })

    it('logs out and goes to the login page when the refresh fails', async () => {
        vi.spyOn(axios, 'post').mockRejectedValue(new Error('refresh token revoked'))
        answerWith(tokenCheckingServer)

        await expect(client.get('/api/Events')).rejects.toThrow('refresh token revoked')

        expect(localStorage.getItem('accessToken')).toBeNull()
        expect(localStorage.getItem('refreshToken')).toBeNull()
        expect(location.href).toBe('/login')
    })

    it('rejects the queued requests too when the refresh fails', async () => {
        let failRefresh!: () => void
        vi.spyOn(axios, 'post').mockImplementation(
            () => new Promise((_, reject) => {
                failRefresh = () => reject(new Error('refresh token revoked'))
            })
        )
        answerWith(tokenCheckingServer)

        const requests = ['/api/Events', '/api/Weather'].map((url) => client.get(url).catch((e: Error) => e.message))
        await vi.waitFor(() => expect(seen).toHaveLength(2))
        failRefresh()

        expect(await Promise.all(requests)).toEqual(['refresh token revoked', 'refresh token revoked'])
        expect(seen).toHaveLength(2) // nothing was retried
    })

    it('can refresh again after an earlier refresh failed', async () => {
        vi.spyOn(axios, 'post').mockRejectedValueOnce(new Error('network down'))
        answerWith(tokenCheckingServer)
        await expect(client.get('/api/Events')).rejects.toThrow('network down')

        localStorage.setItem('accessToken', 'old-access')
        localStorage.setItem('refreshToken', 'stored-refresh')
        refreshSucceeds()

        await expect(client.get('/api/Events')).resolves.toMatchObject({ status: 200 })
    })

    it('logs out without calling the API when there is no refresh token', async () => {
        localStorage.removeItem('refreshToken')
        const post = vi.spyOn(axios, 'post')
        answerWith(tokenCheckingServer)

        await expect(client.get('/api/Events')).rejects.toMatchObject({ response: { status: 401 } })

        expect(post).not.toHaveBeenCalled()
        expect(localStorage.getItem('accessToken')).toBeNull()
        expect(location.href).toBe('/login')
    })
})
