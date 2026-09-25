import DOMPurify from 'dompurify'
import { marked } from 'marked'
import markedKatex from 'marked-katex-extension'

// throwOnError: false keeps a half-streamed or malformed formula as plain source text
// instead of breaking the whole message; output: 'html' skips MathML so DOMPurify's
// default profile keeps the result; nonStandard lets `($x$)` match without spaces.
marked.use(markedKatex({ throwOnError: false, output: 'html', nonStandard: true }))

/** Rewrites `\( … \)` / `\[ … \]` delimiters (which marked would unescape) into `$…$` / `$$…$$`. */
function normalizeMathDelimiters(text: string): string {
  return text
    .replace(/\\\[([\s\S]+?)\\\]/g, (_, math) => `$$${math}$$`)
    .replace(/\\\(([\s\S]+?)\\\)/g, (_, math) => `$${math}$`)
}

/** Renders LLM-generated markdown (with LaTeX math) to sanitized HTML, safe to pass to v-html. */
export function renderMarkdown(text: string): string {
  return DOMPurify.sanitize(marked.parse(normalizeMathDelimiters(text), { async: false }) as string)
}
