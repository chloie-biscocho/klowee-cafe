/*
 * The renderer's shared test cases (handover 21). Byte-identical in client/
 * and admin/, run by formatted.test.ts in each, so the site and the admin's
 * preview can never disagree about what a text means.
 */
import type { Align, Block, Inline } from './formatted'

const t = (text: string): Inline => ({ type: 'text', text })
const p = (children: Inline[], align: Align | null = null): Block => ({ type: 'paragraph', align, children })

export const PARSE_CASES: { name: string; input: string; blocks: Block[] }[] = [
  // ---- each rule ----
  { name: 'empty text has no blocks', input: '', blocks: [] },
  { name: 'plain legacy text is one paragraph, unchanged', input: 'Klowee Cafe started as a two-friend passion project.', blocks: [p([t('Klowee Cafe started as a two-friend passion project.')])] },
  { name: 'a blank line starts a new paragraph', input: 'First.\n\nSecond.', blocks: [p([t('First.')]), p([t('Second.')])] },
  { name: 'a line with only spaces still separates paragraphs', input: 'First.\n   \nSecond.', blocks: [p([t('First.')]), p([t('Second.')])] },
  { name: 'Windows line endings', input: 'First.\r\n\r\nSecond.', blocks: [p([t('First.')]), p([t('Second.')])] },
  { name: 'a single line break stays a line break', input: 'Line one\nLine two', blocks: [p([t('Line one'), { type: 'br' }, t('Line two')])] },
  { name: 'bold', input: 'Big **matcha** energy', blocks: [p([t('Big '), { type: 'strong', children: [t('matcha')] }, t(' energy')])] },
  { name: 'italic', input: 'Big *matcha* energy', blocks: [p([t('Big '), { type: 'em', children: [t('matcha')] }, t(' energy')])] },
  {
    name: 'a link',
    input: 'See [our menu](https://klowee.cafe/#menu) today',
    blocks: [p([t('See '), { type: 'link', href: 'https://klowee.cafe/#menu', children: [t('our menu')] }, t(' today')])],
  },
  {
    name: 'a link with encoded parentheses',
    input: '[the menu](https://klowee.cafe/menu_%28nov%29)',
    blocks: [p([{ type: 'link', href: 'https://klowee.cafe/menu_%28nov%29', children: [t('the menu')] }])],
  },
  { name: 'a mailto link', input: '[Email us](mailto:hello@klowee.cafe)', blocks: [p([{ type: 'link', href: 'mailto:hello@klowee.cafe', children: [t('Email us')] }])] },
  { name: 'a list', input: '- Coffee\n- Matcha', blocks: [{ type: 'list', items: [[t('Coffee')], [t('Matcha')]] }] },
  { name: 'a list item with bold', input: '- **2** baristas', blocks: [{ type: 'list', items: [[{ type: 'strong', children: [t('2')] }, t(' baristas')]] }] },
  {
    name: 'a paragraph then a list in the same block',
    input: 'Includes:\n- Coffee\n- Matcha',
    blocks: [p([t('Includes:')]), { type: 'list', items: [[t('Coffee')], [t('Matcha')]] }],
  },
  { name: 'a dash without a space is not a list', input: '-5 degrees', blocks: [p([t('-5 degrees')])] },

  // ---- alignment ----
  { name: 'justify', input: '::justify\nA long paragraph.', blocks: [p([t('A long paragraph.')], 'justify')] },
  { name: 'center', input: '::center\nCentred.', blocks: [p([t('Centred.')], 'center')] },
  { name: 'left', input: '::left\nLeft.', blocks: [p([t('Left.')], 'left')] },
  {
    name: 'a mix of aligned and plain paragraphs',
    input: '::justify\nOne.\n\nTwo.\n\n::center\nThree.',
    blocks: [p([t('One.')], 'justify'), p([t('Two.')]), p([t('Three.')], 'center')],
  },
  { name: 'a list takes no alignment', input: '::center\n- One\n- Two', blocks: [{ type: 'list', items: [[t('One')], [t('Two')]] }] },
  { name: 'an unknown :: line is shown as typed', input: '::right\nText.', blocks: [p([t('::right'), { type: 'br' }, t('Text.')])] },
  { name: 'an alignment line alone is nothing', input: '::justify', blocks: [] },
  { name: 'an alignment line not first is text', input: 'Text.\n::justify', blocks: [p([t('Text.'), { type: 'br' }, t('::justify')])] },

  // ---- broken and hostile ----
  { name: 'an unclosed bold is two asterisks', input: 'Big **matcha', blocks: [p([t('Big **matcha')])] },
  { name: 'an unclosed italic is an asterisk', input: '5 * 3', blocks: [p([t('5 * 3')])] },
  { name: 'a pair after empty markers is bold', input: '**** and **', blocks: [p([t('**'), { type: 'strong', children: [t(' and ')] }])] },
  { name: 'bare asterisks stay as typed', input: '****', blocks: [p([t('****')])] },
  {
    name: 'italic inside bold',
    input: '**very *good* coffee**',
    blocks: [p([{ type: 'strong', children: [t('very '), { type: 'em', children: [t('good')] }, t(' coffee')] }])],
  },
  {
    name: 'bold inside italic',
    input: '*so **very** good*',
    blocks: [p([{ type: 'em', children: [t('so '), { type: 'strong', children: [t('very')] }, t(' good')] }])],
  },
  { name: 'a script tag is text', input: '<script>alert(1)</script>', blocks: [p([t('<script>alert(1)</script>')])] },
  { name: 'HTML is text', input: '<b onclick="x()">hi</b>', blocks: [p([t('<b onclick="x()">hi</b>')])] },
  { name: 'a javascript: link is text', input: '[click](javascript:alert(1))', blocks: [p([t('[click](javascript:alert(1))')])] },
  { name: 'a javascript: link without parentheses is text', input: '[click](javascript:void0)', blocks: [p([t('[click](javascript:void0)')])] },
  { name: 'a data: link is text', input: '[x](data:text/html,hi)', blocks: [p([t('[x](data:text/html,hi)')])] },
  { name: 'a protocol-relative link is text', input: '[x](//evil.example)', blocks: [p([t('[x](//evil.example)')])] },
  { name: 'a relative link is text', input: '[x](/admin)', blocks: [p([t('[x](/admin)')])] },
  { name: 'a link with a quote is text', input: '[x](https://a.example/"onmouseover=x)', blocks: [p([t('[x](https://a.example/"onmouseover=x)')])] },
  { name: 'an http link with no host is text', input: '[x](https:///path)', blocks: [p([t('[x](https:///path)')])] },
  { name: 'a link inside link text is not made', input: '[a [b](https://x.example)](https://y.example)', blocks: [p([t('[a '), { type: 'link', href: 'https://x.example', children: [t('b')] }, t('](https://y.example)')])] },
  { name: 'a very long line stays one text', input: 'x'.repeat(5000), blocks: [p([t('x'.repeat(5000))])] },
  { name: 'thousands of lone asterisks stay text', input: '*'.repeat(3001), blocks: [p([t('*'.repeat(3001))])] },
]

export const PLAIN_CASES: { name: string; input: string; plain: string }[] = [
  { name: 'markup is removed', input: '::justify\nBig **matcha** and *ube*, see [the menu](https://klowee.cafe).', plain: 'Big matcha and ube, see the menu.' },
  { name: 'paragraphs and list items stay apart', input: 'One.\n\n- a\n- b', plain: 'One.\n\na\nb' },
  { name: 'plain text is unchanged', input: 'Handcrafted coffee.', plain: 'Handcrafted coffee.' },
  { name: 'a refused link keeps its characters', input: '[x](javascript:alert(1))', plain: '[x](javascript:alert(1))' },
]
