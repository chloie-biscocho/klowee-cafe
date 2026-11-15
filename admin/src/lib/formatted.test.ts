import { describe, expect, it } from 'vitest'
import { isSafeUrl, parseFormatted, toPlainText } from './formatted'
import { PARSE_CASES, PLAIN_CASES } from './formattedCases'

// The same file in client/ and admin/: one set of cases for both renderers (handover 21).
describe('formatted text', () => {
  it.each(PARSE_CASES)('$name', ({ input, blocks }) => {
    expect(parseFormatted(input)).toEqual(blocks)
  })

  it.each(PLAIN_CASES)('plain: $name', ({ input, plain }) => {
    expect(toPlainText(input)).toBe(plain)
  })

  it('accepts only http, https and mailto addresses', () => {
    for (const ok of ['https://klowee.cafe', 'http://example.com/a?b=c#d', 'mailto:hello@klowee.cafe', 'HTTPS://EXAMPLE.COM']) expect(isSafeUrl(ok)).toBe(true)
    for (const bad of ['javascript:alert(1)', ' https://x.example', 'https://x.example y', 'ftp://x', '//x.example', '/admin', 'data:text/html,x', 'https://', 'mailto:', 'https://x\u0000y', 'vbscript:x'])
      expect(isSafeUrl(bad)).toBe(false)
  })

  it('stays fast on hostile input', () => {
    const start = performance.now()
    parseFormatted('**a *'.repeat(800))
    parseFormatted('[x]('.repeat(1000))
    expect(performance.now() - start).toBeLessThan(1000)
  })
})
