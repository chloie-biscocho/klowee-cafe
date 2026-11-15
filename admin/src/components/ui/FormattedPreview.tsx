import type { ReactNode } from 'react'
import { parseFormatted, type Align, type Block, type Inline } from '../../lib/formatted'

const ALIGN_CLASS: Record<Align, string> = { left: 'fmt-left', center: 'fmt-center', justify: 'fmt-justify' }

/**
 * A formatted text drawn by the same rules as the site (`lib/formatted.ts`,
 * byte-identical in both apps), in the admin's own type and colours.
 */
export function FormattedPreview({ text }: { text: string }) {
  const blocks = parseFormatted(text)
  if (blocks.length === 0) return <p className="text-sm text-muted">Nothing to preview yet.</p>
  return <div className="fmt flex flex-col gap-2 text-sm leading-relaxed text-ink">{blocks.map(renderBlock)}</div>
}

function renderBlock(block: Block, key: number): ReactNode {
  if (block.type === 'list') {
    return (
      <ul key={key}>
        {block.items.map((item, index) => (
          <li key={index}>{renderInline(item)}</li>
        ))}
      </ul>
    )
  }
  return (
    <p key={key} className={block.align ? ALIGN_CLASS[block.align] : undefined}>
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
