import type { Align } from '../../lib/formatted'

/**
 * What a toolbar button does to the text (handover 21), as pure functions:
 * given the value and the selection, the one range to replace, its new text,
 * and where the selection goes afterwards. `FormattedTextarea` applies the
 * edit as a single browser edit, so Ctrl+Z undoes it in one step.
 */
export interface Edit {
  /** The range of the current value to replace. */
  from: number
  to: number
  insert: string
  /** The selection after the edit, in the new value. */
  selectStart: number
  selectEnd: number
}

const ALIGN_LINE = /^::(justify|center|left)[ \t]*$/

/** Bold (`**`) or italic (`*`): wrap the selection, unwrap it if it's already wrapped, or insert an example. */
export function toggleWrap(value: string, start: number, end: number, marker: '**' | '*', example: string): Edit {
  const selected = value.slice(start, end)
  const m = marker.length
  // The markers sit just outside the selection: take them away.
  if (start >= m && value.slice(start - m, start) === marker && value.slice(end, end + m) === marker && !isOtherMarker(value, start - m, end + m, marker)) {
    return { from: start - m, to: end + m, insert: selected, selectStart: start - m, selectEnd: end - m }
  }
  // The selection includes its markers: take them away.
  if (selected.length > 2 * m && selected.startsWith(marker) && selected.endsWith(marker) && !(marker === '*' && selected.startsWith('**') && selected.endsWith('**'))) {
    const inner = selected.slice(m, -m)
    return { from: start, to: end, insert: inner, selectStart: start, selectEnd: start + inner.length }
  }
  if (start === end) {
    return { from: start, to: end, insert: `${marker}${example}${marker}`, selectStart: start + m, selectEnd: start + m + example.length }
  }
  // Markers hug the words: spaces at the edges of the selection stay outside.
  const lead = selected.length - selected.trimStart().length
  const trail = selected.length - selected.trimEnd().length
  const core = selected.trim()
  if (!core) return { from: start, to: end, insert: selected, selectStart: start, selectEnd: end }
  const insert = `${selected.slice(0, lead)}${marker}${core}${marker}${selected.slice(selected.length - trail)}`
  return { from: start, to: end, insert, selectStart: start + lead + m, selectEnd: start + lead + m + core.length }
}

/** An italic "*" that is really half of a bold "**" isn't an italic marker. */
function isOtherMarker(value: string, open: number, closeEnd: number, marker: '**' | '*'): boolean {
  return marker === '*' && (value[open - 1] === '*' || value[closeEnd] === '*')
}

/** A link: `[selection](url)`, an example text when nothing is selected, or the text alone when the selection is a link. */
export function toggleLink(value: string, start: number, end: number, url: string | null): Edit {
  const selected = value.slice(start, end)
  const existing = /^\[([^[\]]+)\]\(([^()\s]+)\)$/.exec(selected)
  if (existing) {
    const words = existing[1]!
    return { from: start, to: end, insert: words, selectStart: start, selectEnd: start + words.length }
  }
  const words = selected.trim() || 'link text'
  const insert = `[${words}](${url ?? ''})`
  return { from: start, to: end, insert, selectStart: start + 1, selectEnd: start + 1 + words.length }
}

/** Is the selection exactly a link, so Link removes it rather than asking for an address? */
export function selectionIsLink(value: string, start: number, end: number): boolean {
  return /^\[([^[\]]+)\]\(([^()\s]+)\)$/.test(value.slice(start, end))
}

/** The whole lines the selection touches. */
function lineRange(value: string, start: number, end: number): [number, number] {
  const from = value.lastIndexOf('\n', start - 1) + 1
  const lastNewline = value.indexOf('\n', end > start && value[end - 1] === '\n' ? end - 1 : end)
  return [from, lastNewline === -1 ? value.length : lastNewline]
}

/** A bullet list: "- " in front of each touched line, or off again when every touched line has one. */
export function toggleList(value: string, start: number, end: number): Edit {
  const [from, to] = lineRange(value, start, end)
  const lines = value.slice(from, to).split('\n')
  const filled = lines.filter((line) => line.trim() !== '')
  const allListed = filled.length > 0 && filled.every((line) => line.startsWith('- '))
  if (lines.length === 1 && lines[0] === '') {
    return { from, to, insert: '- ', selectStart: from + 2, selectEnd: from + 2 }
  }
  const changed = lines.map((line) => (line.trim() === '' ? line : allListed ? line.slice(2) : line.startsWith('- ') ? line : `- ${line}`))
  const insert = changed.join('\n')
  return { from, to, insert, selectStart: from, selectEnd: from + insert.length }
}

interface Paragraph {
  /** Where the paragraph's first line starts and its last line ends. */
  start: number
  end: number
  align: Align | null
}

/** The paragraphs (blocks between blank lines) of the text, with their alignment line if any. */
export function paragraphsOf(value: string): Paragraph[] {
  const out: Paragraph[] = []
  const re = /[^\n]+(?:\n(?![ \t]*(?:\n|$))[^\n]*)*/g
  for (let match = re.exec(value); match; match = re.exec(value)) {
    if (match[0].trim() === '') continue
    const firstLine = match[0].split('\n')[0]!.trim()
    const marker = ALIGN_LINE.exec(firstLine)
    out.push({ start: match.index, end: match.index + match[0].length, align: marker ? (marker[1] as Align) : null })
  }
  return out
}

/** The alignment the cursor's paragraph has: its marker, or left when it has none. */
export function alignmentAt(value: string, position: number): Align {
  const paragraph = paragraphsOf(value).find((p) => position >= p.start && position <= p.end)
  return paragraph?.align ?? 'left'
}

/**
 * Left, Centre or Justify on every paragraph the selection touches: set it,
 * change it, or (pressing the paragraph's own alignment, or Left) remove the
 * marker, since a paragraph without one is left-aligned.
 */
export function setAlignment(value: string, start: number, end: number, target: Align): Edit {
  const touched = paragraphsOf(value).filter((p) => p.end >= start && p.start <= Math.max(start, end))
  if (touched.length === 0) {
    // An empty spot: start a paragraph with the marker.
    if (target === 'left') return { from: start, to: end, insert: '', selectStart: start, selectEnd: start }
    const insert = `::${target}\n`
    return { from: start, to: start, insert, selectStart: start + insert.length, selectEnd: start + insert.length }
  }
  const from = touched[0]!.start
  const to = touched[touched.length - 1]!.end
  let text = value.slice(from, to)
  let shiftStart = 0
  let shiftEnd = 0
  // Back to front, so earlier offsets stay valid.
  for (const paragraph of [...touched].reverse()) {
    const at = paragraph.start - from
    const firstLineEnd = text.indexOf('\n', at)
    const remove = paragraph.align ? (firstLineEnd === -1 ? text.length : firstLineEnd + 1) - at : 0
    const turnOff = target === 'left' || paragraph.align === target
    const add = turnOff ? '' : `::${target}\n`
    text = text.slice(0, at) + add + text.slice(at + remove)
    const delta = add.length - remove
    if (paragraph.start <= start) shiftStart += delta
    if (paragraph.start <= end) shiftEnd += delta
  }
  return { from, to, insert: text, selectStart: Math.max(from, start + shiftStart), selectEnd: Math.max(from, end + shiftEnd) }
}

/** Applies an edit to a string (for tests and the fallback path). */
export function applyEdit(value: string, edit: Edit): string {
  return value.slice(0, edit.from) + edit.insert + value.slice(edit.to)
}
