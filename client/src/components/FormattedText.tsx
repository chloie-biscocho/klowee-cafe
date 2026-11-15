import type { ReactNode } from 'react'
import { parseFormatted, type Align, type Block, type Inline } from '../lib/formatted'

const ALIGN_CLASS: Record<Align, string> = {
  left: 'fmt-left',
  center: 'fmt-center',
  justify: 'fmt-justify',
}

/**
 * One of the owners' longer texts, drawn by the shared rules (handover 21,
 * `lib/formatted.ts`): paragraphs with their alignment, line breaks, bold,
 * italic, safe links (new tab) and bullet lists. Plain text comes out as the
 * same `<p>` elements, with the same classes, as before.
 *
 * The wrapper is a size container, so a justified paragraph goes left-aligned
 * when its own column is narrower than about 420px (index.css), whatever the
 * screen: wide gaps between words come from a narrow column, not a small phone.
 */
export function FormattedText({
  text,
  className = '',
  paragraphClassName = '',
  listClassName = '',
}: {
  text: string | null | undefined
  className?: string
  paragraphClassName?: string
  listClassName?: string
}) {
  const blocks = parseFormatted(text)
  if (blocks.length === 0) return null
  return <div className={`fmt ${className}`}>{blocks.map((block, index) => renderBlock(block, index, paragraphClassName, listClassName))}</div>
}

function renderBlock(block: Block, key: number, paragraphClassName: string, listClassName: string): ReactNode {
  if (block.type === 'list') {
    return (
      <ul key={key} className={listClassName}>
        {block.items.map((item, index) => (
          <li key={index}>{renderInline(item)}</li>
        ))}
      </ul>
    )
  }
  const align = block.align ? ALIGN_CLASS[block.align] : ''
  return (
    <p key={key} className={`${paragraphClassName} ${align}`.trim() || undefined}>
      {renderInline(block.children)}
    </p>
  )
}

function renderInline(nodes: Inline[]): ReactNode[] {
  return nodes.map((node, index) => {
    switch (node.type) {
      case 'text':
        return node.text
      case 'br':
        return <br key={index} />
      case 'strong':
        return <strong key={index}>{renderInline(node.children)}</strong>
      case 'em':
        return <em key={index}>{renderInline(node.children)}</em>
      case 'link':
        return (
          <a key={index} href={node.href} target="_blank" rel="noopener noreferrer">
            {renderInline(node.children)}
          </a>
        )
    }
  })
}
