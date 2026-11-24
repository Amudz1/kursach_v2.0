import { shallowRef } from 'vue'
import { explain } from './api'
import { showToast } from './components/ui/use-toast'
import { useSession } from './session'
import type { InterfaceVariant } from './types'

type Interrupt = () => void | Promise<void>
type SwitchRequest = { variant: InterfaceVariant; owner: string }
let readVoiceOperation: (() => Interrupt | undefined) | undefined
export const interfaceSwitchConfirmation = shallowRef<(SwitchRequest & { interrupt: Interrupt })>()

// The voice session can provide a cancellation callback for its unfinished operation.
export function registerInterfaceSwitchGuard(read: () => Interrupt | undefined) {
  readVoiceOperation = read
  return () => { if (readVoiceOperation === read) readVoiceOperation = undefined }
}

export function requestInterfaceSwitch(variant: InterfaceVariant) {
  const session = useSession()
  const owner = session.account?.id
  if (!owner || session.interfaceSaving) return
  if (session.interfaceVariant === variant && session.savedInterfaceVariant === variant) return
  const interrupt = readVoiceOperation?.()
  if (interrupt) interfaceSwitchConfirmation.value = { variant, owner, interrupt }
  else void saveSwitch({ variant, owner })
}

async function saveSwitch(request: SwitchRequest) {
  const session = useSession()
  if (session.account?.id !== request.owner) return
  try { await session.setInterfaceVariant(request.variant) }
  catch (error) {
    if (session.account?.id !== request.owner) return
    showToast(`Не удалось сменить интерфейс. ${explain(error)}`, 'danger', {
      action: { label: 'Повторить', run: () => requestInterfaceSwitch(request.variant) },
    })
  }
}

export async function confirmInterfaceSwitch() {
  const request = interfaceSwitchConfirmation.value
  interfaceSwitchConfirmation.value = undefined
  if (!request || useSession().account?.id !== request.owner) return
  try {
    await request.interrupt()
    await saveSwitch(request)
  } catch (error) { showToast(explain(error), 'danger') }
}
