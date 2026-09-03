import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AuthProvider, useAuth } from './AuthContext'
import { saveSessionSnapshot, loadSessionSnapshot } from '@/lib/session-cache'
import * as authApi from '@/api/auth'
import type { User } from '@/types/auth'

const { mockAxiosPost } = vi.hoisted(() => ({ mockAxiosPost: vi.fn() }))

vi.mock('axios', async (importOriginal) => {
  const actual = await importOriginal<typeof import('axios')>()
  return { ...actual, default: { ...actual.default, post: mockAxiosPost } }
})

vi.mock('@/api/auth')

const testUser: User = {
  id: 'u1',
  email: 'ama@shop.test',
  firstName: 'Ama',
  lastName: 'Owusu',
  isEmailVerified: true,
  photoUrl: null,
  businesses: [],
}

function TestConsumer() {
  const { user, isInitializing } = useAuth()
  if (isInitializing) return <div>loading</div>
  return <div>{user ? `signed in as ${user.email}` : 'signed out'}</div>
}

function LoginTestConsumer() {
  const { user, login } = useAuth()
  return (
    <div>
      <div>{user ? `signed in as ${user.email}` : 'signed out'}</div>
      <button onClick={() => void login('ama@shop.test', 'password123')}>sign in</button>
    </div>
  )
}

/** A cold start renders from the cached session snapshot as soon as that (fast, local) read
 * resolves, without waiting on the network check at all - "sign in once, stay signed in" rather
 * than re-verifying against the server on every launch. The background /auth/refresh call still
 * runs to rotate the token and catch a real remote logout, but must never downgrade a cached
 * session just because it can't reach the server - that's the whole point for a native app that
 * cold-starts offline. */
describe('AuthProvider mount-time session check', () => {
  beforeEach(() => {
    localStorage.clear()
    mockAxiosPost.mockReset()
  })

  // api-client.ts memoizes the in-flight refresh call in a module-level variable, cleared via
  // .finally() once it settles - deliberately, to dedupe concurrent real callers. If a test ends
  // while that promise is still pending, the next test's render() reuses the stale, already-
  // rejected/resolved-for-a-different-test promise instead of making its own call. Draining
  // every promise the mock returned this test guarantees that .finally() has already fired
  // (registered before this awaits the same promise, so it always runs first) before moving on.
  afterEach(async () => {
    await Promise.allSettled(mockAxiosPost.mock.results.map((r) => (r.type === 'return' ? r.value : undefined)))
    localStorage.clear()
  })

  it('signs the user in when the refresh call succeeds', async () => {
    mockAxiosPost.mockResolvedValueOnce({
      data: { accessToken: 'tok', accessTokenExpiresAt: '2030-01-01', user: testUser },
    })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(await screen.findByText('signed in as ama@shop.test')).toBeInTheDocument()
  })

  it('renders the cached session even while the background refresh is still pending', async () => {
    await saveSessionSnapshot(testUser, null)
    // Never settles within this test - if the cached render depended on this resolving, the
    // test would time out waiting for "signed in", instead of finding it via the cache alone.
    let releaseRefresh!: () => void
    mockAxiosPost.mockReturnValueOnce(new Promise((resolve) => (releaseRefresh = () => resolve({ data: {} }))))

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(await screen.findByText('signed in as ama@shop.test')).toBeInTheDocument()

    // Release it before the test ends so it doesn't leak a pending dedupedRefresh promise into
    // the next test (see the afterEach comment above).
    releaseRefresh()
  })

  it('keeps showing the cached session when the background refresh fails with a network error', async () => {
    await saveSessionSnapshot(testUser, null)
    // No `.response` on the rejection - exactly what axios produces for a genuinely
    // unreachable server (offline, DNS failure, timeout), as opposed to a real 4xx/5xx.
    mockAxiosPost.mockRejectedValueOnce(new Error('Network Error'))

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(await screen.findByText('signed in as ama@shop.test')).toBeInTheDocument()
    // Give the rejected background call a tick to settle, then confirm it didn't downgrade us.
    await waitFor(() => expect(mockAxiosPost).toHaveBeenCalled())
    expect(screen.getByText('signed in as ama@shop.test')).toBeInTheDocument()
  })

  it('shows signed-out (not a stuck loader) on a network error with no cached session', async () => {
    mockAxiosPost.mockRejectedValueOnce(new Error('Network Error'))

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    expect(await screen.findByText('signed out')).toBeInTheDocument()
  })

  it('treats a real 401 as an actual logout, overriding a cached session, and clears the cache', async () => {
    await saveSessionSnapshot(testUser, null)
    mockAxiosPost.mockRejectedValueOnce({ response: { status: 401, data: { title: 'Unauthorized' } } })

    render(
      <AuthProvider>
        <TestConsumer />
      </AuthProvider>,
    )

    // May briefly show signed-in from the cache first, depending on exactly how the cache
    // read and the network rejection interleave - not asserted here since real timing (a local
    // read always beats a network round-trip) makes that order reliable in practice even though
    // two independently-mocked promises in a test don't guarantee it. What must always hold:
    // the real 401 wins in the end, regardless of arrival order.
    expect(await screen.findByText('signed out')).toBeInTheDocument()
    await waitFor(async () => expect(await loadSessionSnapshot()).toBeNull())
  })
})

/** The mount-time tests above prove the *read* side (cache -> instant render) in isolation by
 * seeding localStorage directly - they don't prove login() actually ever *writes* a snapshot
 * for a real sign-in to read back later. That write path is exactly where a live desktop-app
 * test showed nothing was ever persisted, so it needs its own coverage rather than being
 * assumed from the mount tests passing. */
describe('AuthProvider persists a session snapshot after a real login', () => {
  beforeEach(() => {
    localStorage.clear()
    mockAxiosPost.mockReset()
    vi.mocked(authApi.login).mockReset()
  })

  afterEach(async () => {
    await Promise.allSettled(mockAxiosPost.mock.results.map((r) => (r.type === 'return' ? r.value : undefined)))
    localStorage.clear()
  })

  it('saves a snapshot immediately after login() resolves, readable on the next cold start', async () => {
    // No cache yet - mount resolves unauthenticated (no live session either), same as any
    // never-before-logged-in device.
    mockAxiosPost.mockRejectedValueOnce({ response: { status: 401, data: { title: 'Unauthorized' } } })
    vi.mocked(authApi.login).mockResolvedValue({
      requiresTwoFactor: false,
      auth: { accessToken: 'tok', accessTokenExpiresAt: '2030-01-01', user: testUser },
    })

    render(
      <AuthProvider>
        <LoginTestConsumer />
      </AuthProvider>,
    )

    expect(await loadSessionSnapshot()).toBeNull()

    await userEvent.click(screen.getByText('sign in'))

    await screen.findByText('signed in as ama@shop.test')
    await waitFor(async () => expect((await loadSessionSnapshot())?.user.email).toBe('ama@shop.test'))
  })
})
