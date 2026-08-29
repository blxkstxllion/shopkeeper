import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { DashboardHeader } from './DashboardHeader'
import * as authApi from '@/api/auth'
import * as AuthContextModule from '@/contexts/AuthContext'
import type { AuthContextValue } from '@/contexts/AuthContext'

vi.mock('@/api/auth')
vi.mock('@/contexts/AuthContext', async (importOriginal) => {
  const actual = await importOriginal<typeof AuthContextModule>()
  return { ...actual, useAuth: vi.fn() }
})

const refreshUser = vi.fn()

function mockAuthValue(overrides: Partial<AuthContextValue> = {}) {
  vi.mocked(AuthContextModule.useAuth).mockReturnValue({
    user: {
      id: '1',
      email: 'ama@shop.test',
      firstName: 'Ama',
      lastName: 'Owusu',
      isEmailVerified: true,
      photoUrl: null,
      businesses: [],
    },
    activeBusiness: {
      businessId: 'b1',
      businessName: 'Ama Shop',
      roleName: 'Owner',
      isOwner: true,
      onboardingCompleted: true,
      currencyCode: 'GHS',
      colorTheme: 'green',
    },
    isInitializing: false,
    login: vi.fn(),
    completeTwoFactorLogin: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    selectBusiness: vi.fn(),
    completeOnboarding: vi.fn(),
    applyAuthResult: vi.fn(),
    refreshUser,
    ...overrides,
  })
}

describe('DashboardHeader', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockAuthValue()
  })

  it('renders the welcome greeting with role and business name', () => {
    render(
      <MemoryRouter>
        <DashboardHeader />
      </MemoryRouter>,
    )

    expect(screen.getByText(/Welcome back, Ama/)).toBeInTheDocument()
    expect(screen.getByText(/Ama Shop/)).toBeInTheDocument()
  })

  it('uploads a selected photo, saves it, and refreshes the user', async () => {
    vi.mocked(authApi.uploadProfilePhoto).mockResolvedValue({ url: '/uploads/profile-photos/x.jpg' })
    vi.mocked(authApi.updateProfilePhoto).mockResolvedValue(undefined)

    render(
      <MemoryRouter>
        <DashboardHeader />
      </MemoryRouter>,
    )
    const file = new File(['fake-image-bytes'], 'me.jpg', { type: 'image/jpeg' })
    await userEvent.upload(screen.getByLabelText('Upload profile photo'), file)

    await waitFor(() => expect(refreshUser).toHaveBeenCalledTimes(1))
    expect(authApi.uploadProfilePhoto).toHaveBeenCalledWith(file)
    expect(authApi.updateProfilePhoto).toHaveBeenCalledWith('/uploads/profile-photos/x.jpg')
  })

  it('rejects an unsupported file type before ever calling the upload API', async () => {
    render(
      <MemoryRouter>
        <DashboardHeader />
      </MemoryRouter>,
    )
    const file = new File(['not an image'], 'me.txt', { type: 'text/plain' })
    // fireEvent, not userEvent.upload - userEvent respects the input's `accept` attribute and
    // silently filters mismatched files the way a real OS file picker would, so it would never
    // even fire onChange here. fireEvent exercises the component's own validation directly,
    // which still matters for drag-and-drop or browsers that don't enforce `accept`.
    fireEvent.change(screen.getByLabelText('Upload profile photo'), { target: { files: [file] } })

    expect(await screen.findByText(/JPEG, PNG, WEBP, or GIF/)).toBeInTheDocument()
    expect(authApi.uploadProfilePhoto).not.toHaveBeenCalled()
    expect(refreshUser).not.toHaveBeenCalled()
  })
})
