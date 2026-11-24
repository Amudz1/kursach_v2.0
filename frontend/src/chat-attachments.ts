export const attachmentExtensions = ['.txt', '.md', '.pdf', '.docx', '.png', '.jpg', '.jpeg']
export const maxAttachmentCount = 4
export const maxAttachmentBytes = 16 * 1024 * 1024

export function validateAttachmentSelection(
  existing: File[], incoming: File[], planLimit = maxAttachmentBytes,
) {
  const accepted: File[] = []
  const rejected: { name: string; message: string }[] = []
  let total = existing.reduce((sum, file) => sum + file.size, 0)
  for (const file of incoming) {
    const name = file.name.trim()
    const extension = name.match(/\.[^.]+$/)?.[0].toLowerCase()
    let message = ''
    if (!name || name.length > 255 || /[\u0000-\u001f\u007f-\u009f]/.test(name)) message = 'Проверьте имя файла.'
    else if (!extension || !attachmentExtensions.includes(extension)) message = 'Допустимые форматы: TXT, MD, PDF, DOCX, PNG и JPEG.'
    else if (!file.size) message = 'Файл пуст.'
    else if (file.size > maxAttachmentBytes) message = 'Максимальный размер файла — 16 МБ.'
    else if (existing.length + accepted.length >= maxAttachmentCount) message = 'Можно прикрепить до четырёх файлов.'
    else if (total + file.size > planLimit) message = 'С этим файлом общий размер вложений превысит лимит тарифа.'
    if (message) rejected.push({ name: name || 'Файл без имени', message })
    else {
      total += file.size
      accepted.push(file)
    }
  }
  return { accepted, rejected }
}
