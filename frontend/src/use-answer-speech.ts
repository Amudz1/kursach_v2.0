import { onScopeDispose, ref } from 'vue'

const partLimit = 200
const unavailable = 'Озвучивание недоступно на этом устройстве.'

function makeParts(text: string) {
  const parts: string[] = []
  for (const block of text.split('\n')) {
    let line = block.trim().replace(/\s+/g, ' ')
    while (line.length > partLimit) {
      const space = line.lastIndexOf(' ', partLimit)
      const cut = space > 0 ? space : partLimit
      parts.push(line.slice(0, cut))
      line = line.slice(cut).trimStart()
    }
    if (line) parts.push(line)
  }
  return parts
}

function formatForReading(content: HTMLElement, english: boolean) {
  const copy = content.cloneNode(true) as HTMLElement
  for (const code of copy.querySelectorAll('.code-frame, pre')) {
    if (!copy.contains(code)) continue
    const label = copy.ownerDocument.createElement('p')
    label.textContent = english ? 'Code block.' : 'Блок кода.'
    code.replaceWith(label)
  }
  for (const item of copy.querySelectorAll('p, li, h1, h2, h3, h4, h5, h6, blockquote, tr, br')) {
    item.after(copy.ownerDocument.createTextNode('\n'))
  }
  return (copy.textContent ?? '').replace(/(?:https?:\/\/|www\.)[^\s<>()]+/gi, english ? 'link' : 'ссылка')
}

function answerLanguage(content: HTMLElement, known?: string) {
  if (known === 'en' || known === 'ru') return known
  return content.ownerDocument.documentElement.lang || 'ru'
}

export function useAnswerSpeech() {
  const activeId = ref('')
  const paused = ref(false)
  const notice = ref<{ id: string; message: string }>()
  let revision = 0
  let utterance: SpeechSynthesisUtterance | undefined

  function stop() {
    revision++
    if (activeId.value) window.speechSynthesis?.cancel()
    activeId.value = ''
    paused.value = false
    utterance = undefined
  }

  function start(id: string, content: HTMLElement, knownLanguage?: string) {
    stop()
    notice.value = undefined
    const engine = window.speechSynthesis
    const language = answerLanguage(content, knownLanguage).toLowerCase()
    const base = language.split('-')[0]
    const voices = (engine?.getVoices() ?? []).filter(voice => voice.localService)
    const voice = voices.find(voice => voice.lang.toLowerCase() === language) ??
      voices.find(voice => voice.lang.toLowerCase().split('-')[0] === base)
    if (!engine || !voice || typeof SpeechSynthesisUtterance === 'undefined') {
      notice.value = { id, message: unavailable }
      return
    }
    const parts = makeParts(formatForReading(content, base === 'en'))
    if (!parts.length) return
    const operation = revision
    activeId.value = id
    let index = 0
    function readNext() {
      if (operation !== revision) return
      if (index === parts.length) { stop(); return }
      utterance = new SpeechSynthesisUtterance(parts[index++])
      utterance.lang = voice!.lang
      utterance.voice = voice!
      utterance.onend = readNext
      utterance.onerror = () => {
        if (operation !== revision) return
        stop()
        notice.value = { id, message: unavailable }
      }
      engine.speak(utterance)
    }
    if (engine.paused) engine.resume()
    readNext()
  }

  function togglePause() {
    if (!activeId.value) return
    if (paused.value) window.speechSynthesis.resume()
    else window.speechSynthesis.pause()
    paused.value = !paused.value
  }

  onScopeDispose(stop)
  return { activeId, paused, notice, start, stop, togglePause }
}

export type AnswerSpeech = ReturnType<typeof useAnswerSpeech>
