/** Tailwind aspect classes for every image slot on the page. */
export type Aspect = 'square' | 'wide' | 'hero' | 'polaroid'

export const ASPECTS: Record<Aspect, string> = {
  square: 'aspect-square',
  wide: 'aspect-[4/3]',
  hero: 'aspect-[16/7]',
  polaroid: 'aspect-[4/5]',
}
