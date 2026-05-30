import axios from 'axios'
import { getToken, clearStoredAuth } from './auth-token'
import { normalizeError } from './problem-details'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
})

// Attach the bearer token to every request.
http.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Normalize errors; auto-logout on an expired/invalid token.
http.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status

    // Only treat 401 as a session expiry when we actually had a token
    // (a failed login also returns 401 but must surface to the form).
    if (status === 401 && getToken()) {
      clearStoredAuth()
      // Dynamic import avoids a static import cycle (router -> store -> api -> http).
      void import('@/router').then(({ default: router }) => {
        const current = router.currentRoute.value
        if (current.name !== 'login') {
          router.push({ name: 'login', query: { redirect: current.fullPath } })
        }
      })
    }

    return Promise.reject(normalizeError(error))
  },
)
