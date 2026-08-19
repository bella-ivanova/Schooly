import DOMPurify from 'dompurify'
import { marked } from 'marked'

/** Renders LLM-generated markdown to sanitized HTML, safe to pass to v-html. */
export function renderMarkdown(text: string): string {
  return DOMPurify.sanitize(marked.parse(text, { async: false }) as string)
}
