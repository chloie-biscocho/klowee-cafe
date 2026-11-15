/*
 * Formatted text (handover 21, decision 024): a small, fixed subset of
 * Markdown, stored as the plain text it is. This file is byte-identical in
 * client/src/lib/formatted.ts and admin/src/lib/formatted.ts (the two apps
 * never import from each other), and both run the cases in formattedCases.ts.
 *
 *   **bold**   *italic*   [words](https://…)   "- " list lines
 *   a blank line: a new paragraph      a single line break: a line break
 *   a paragraph whose first line is ::justify, ::center or ::left: its alignment
 *
 * The output is a tree of plain data. Text only ever becomes a text node
 * (React escapes it), so "<script>" stays the characters typed: nothing is
 * HTML, at any point. Unknown or broken markup is shown as typed.
 */

export type Align = 'left' | 'center' | 'justify'

export type Inline =
  | { type: 'text'; text: string }
  | { type: 'strong'; children: Inline[] }
  | { type: 'em'; children: Inline[] }
  | { type: 'link'; href: string; children: Inline[] }
  | { type: 'br' }

export type Block =
  /** `align` null: no marker, so the paragraph keeps whatever its place on the page gives it. */
  | { type: 'paragraph'; align: Align | null; children: Inline[] }
  | { type: 'list'; items: Inline[][] }

const ALIGN_LINE = /^::(justify|center|left)\s*$/
const LIST_LINE = /^- /

/**
 * Only absolute http(s) addresses with a host, and mailto. No spaces, quotes,
 * angle brackets or control characters anywhere; checked against the whole
 * string, so "javascript:", "//host" and relative paths never pass.
 */
// Control characters are matched on purpose: they are refused.
// eslint-disable-next-line no-control-regex
const SAFE_URL = /^(?:https?:\/\/[^\s/?#"'<>`\\\u0000-\u001f\u007f][^\s"'<>`\\\u0000-\u001f\u007f]*|mailto:[^\s"'<>`\\\u0000-\u001f\u007f]+)$/i

export function isSafeUrl(url: string): boolean {
  return SAFE_URL.test(url)
}

/** The text as blocks: paragraphs (with their alignment) and lists. */
export function parseFormatted(source: string | null | undefined): Block[] {
  const text = (source ?? '').replace(/\r\n?/g, '\n')
  const blocks: Block[] = []

  // Paragraphs are separated by a blank line (whitespace only counts as blank).
  for (const chunk of text.split(/\n[ \t]*\n/)) {
    const lines = chunk.split('\n')
    while (lines.length > 0 && lines[0]!.trim() === '') lines.shift()
    while (lines.length > 0 && lines[lines.length - 1]!.trim() === '') lines.pop()
    if (lines.length === 0) continue

    let align: Align | null = null
    const marker = ALIGN_LINE.exec(lines[0]!.trim())
    if (marker) {
      align = marker[1] as Align
      lines.shift()
      if (lines.length === 0) continue
    }

    // Runs of "- " lines are lists (which take no alignment); the lines between them are paragraphs.
    let run: string[] = []
    let runIsList = false
    const flush = () => {
      if (run.length === 0) return
      if (runIsList) blocks.push({ type: 'list', items: run.map((line) => parseInline(line.slice(2))) })
      else blocks.push({ type: 'paragraph', align, children: joinLines(run) })
      run = []
    }
    for (const line of lines) {
      const isList = LIST_LINE.test(line)
      if (run.length > 0 && isList !== runIsList) flush()
      runIsList = isList
      run.push(line)
    }
    flush()
  }

  return blocks
}

function joinLines(lines: string[]): Inline[] {
  const out: Inline[] = []
  lines.forEach((line, index) => {
    if (index > 0) out.push({ type: 'br' })
    out.push(...parseInline(line))
  })
  return out
}

/** Bold, italic and links on one line. A marker with no partner is just a character. */
export function parseInline(line: string, allowLinks = true): Inline[] {
  const out: Inline[] = []
  let plain = ''
  const pushPlain = () => {
    if (plain) out.push({ type: 'text', text: plain })
    plain = ''
  }

  let i = 0
  while (i < line.length) {
    if (line.startsWith('**', i)) {
      const close = line.indexOf('**', i + 2)
      if (close > i + 2) {
        pushPlain()
        out.push({ type: 'strong', children: parseInline(line.slice(i + 2, close), allowLinks) })
        i = close + 2
        continue
      }
      plain += '**'
      i += 2
      continue
    }
    if (line[i] === '*') {
      const close = findItalicClose(line, i + 1)
      if (close > i + 1) {
        pushPlain()
        out.push({ type: 'em', children: parseInline(line.slice(i + 1, close), allowLinks) })
        i = close + 1
        continue
      }
      plain += '*'
      i += 1
      continue
    }
    if (allowLinks && line[i] === '[') {
      const link = /^\[([^[\]]+)\]\(([^()\s]+)\)/.exec(line.slice(i))
      if (link && isSafeUrl(link[2]!)) {
        pushPlain()
        out.push({ type: 'link', href: link[2]!, children: parseInline(link[1]!, false) })
        i += link[0].length
        continue
      }
    }
    plain += line[i]
    i += 1
  }
  pushPlain()
  return out
}

/** The next single "*" that closes an italic (a "**" pair inside is bold, skipped whole). */
function findItalicClose(line: string, from: number): number {
  let j = from
  while (j < line.length) {
    if (line.startsWith('**', j)) {
      const close = line.indexOf('**', j + 2)
      if (close === -1) return -1
      j = close + 2
      continue
    }
    if (line[j] === '*') return j
    j += 1
  }
  return -1
}

function inlineText(nodes: Inline[]): string {
  return nodes.map((node) => (node.type === 'text' ? node.text : node.type === 'br' ? '\n' : inlineText(node.children))).join('')
}

/**
 * The same text with the markup taken out, for places that must stay plain
 * (an alt, a meta description, a one-line excerpt in an admin table).
 * Paragraphs are separated by a blank line, list items by line breaks.
 */
export function toPlainText(source: string | null | undefined): string {
  return parseFormatted(source)
    .map((block) => (block.type === 'paragraph' ? inlineText(block.children) : block.items.map(inlineText).join('\n')))
    .join('\n\n')
}
