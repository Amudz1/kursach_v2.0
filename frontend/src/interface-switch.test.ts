import { afterEach, expect, it, vi } from 'vitest'
import { confirmInterfaceSwitch, interfaceSwitchConfirmation, registerInterfaceSwitchGuard, requestInterfaceSwitch } from './interface-switch'

const mocks = vi.hoisted(() => ({
  session: {
    account: { id: 'first' }, interfaceSaving: false, interfaceVariant: 'current', savedInterfaceVariant: 'current',
    setInterfaceVariant: vi.fn(),
  },
  toast: vi.fn(),
}))
vi.mock('./session', () => ({ useSession: () => mocks.session }))
vi.mock('./components/ui/use-toast', () => ({ showToast: mocks.toast }))

afterEach(() => {
  mocks.session.account.id = 'first'
  interfaceSwitchConfirmation.value = undefined
  vi.clearAllMocks()
})

it('waits for confirmation and interrupts a voice operation before switching', async () => {
  const calls: string[] = []
  mocks.session.setInterfaceVariant.mockImplementation(() => { calls.push('switch') })
  const unregister = registerInterfaceSwitchGuard(() => async () => { calls.push('interrupt') })
  try {
    requestInterfaceSwitch('focus')
    expect(interfaceSwitchConfirmation.value?.variant).toBe('focus')
    expect(calls).toEqual([])
    interfaceSwitchConfirmation.value = undefined
    expect(calls).toEqual([])

    requestInterfaceSwitch('focus')
    await confirmInterfaceSwitch()
    expect(calls).toEqual(['interrupt', 'switch'])
  } finally { unregister() }
})

it('does not reuse a pending quick switch under a different account', async () => {
  const interrupt = vi.fn()
  const unregister = registerInterfaceSwitchGuard(() => interrupt)
  try {
    requestInterfaceSwitch('focus')
    mocks.session.account.id = 'second'
    await confirmInterfaceSwitch()
    expect(interrupt).not.toHaveBeenCalled()
    expect(mocks.session.setInterfaceVariant).not.toHaveBeenCalled()
  } finally { unregister() }
})
