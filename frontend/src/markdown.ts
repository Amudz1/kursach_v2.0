import DOMPurify from 'dompurify'
import hljs from 'highlight.js/lib/core'
import csharp from 'highlight.js/lib/languages/csharp'
import javascript from 'highlight.js/lib/languages/javascript'
import typescript from 'highlight.js/lib/languages/typescript'
import json from 'highlight.js/lib/languages/json'
import sql from 'highlight.js/lib/languages/sql'
import bash from 'highlight.js/lib/languages/bash'
import plaintext from 'highlight.js/lib/languages/plaintext'
import { Marked } from 'marked'

for (const [name, language] of Object.entries({ csharp, javascript, typescript, json, sql, bash, plaintext })) {
  hljs.registerLanguage(name, language)
}
const aliases: Record<string, string> = { cs: 'csharp', 'c#': 'csharp', js: 'javascript', ts: 'typescript', sh: 'bash' }
const parser = new Marked({
  renderer: {
    code({ text, lang }) {
      const first = lang?.trim().toLowerCase().split(/\s+/)[0] ?? ''
      const name = aliases[first] ?? first
      const language = hljs.getLanguage(name) && text.length < 30000 ? name : 'plaintext'
      const value = hljs.highlight(text, { language, ignoreIllegals: true }).value
      return `<pre><code class="hljs">${value}</code></pre>`
    },
  },
})

export function markdown(text: string) {
  return DOMPurify.sanitize(parser.parse(text, { async: false }), {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['img', 'style', 'form', 'video', 'audio', 'iframe', 'object'],
    FORBID_ATTR: ['style', 'srcset'],
  })
}
