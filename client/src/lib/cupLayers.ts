/**
 * How each drink looks as a cup on the shelf: three layers, top to bottom
 * (`a`, `b`, `c`), and where they meet (`s1`, `s2`, from the top of the glass).
 *
 * This is presentational, not data. The colours are an illustration of each
 * drink, seeded from the approved mockup's 25 cups, and live in the client
 * because nothing else needs them. A drink that isn't listed here still gets a
 * cup: it renders with DEFAULT_LAYERS. Adding a new drink to the menu never
 * breaks the shelf; giving it its own colours is a one-line change below.
 * Keys are base names, normalised with `cupKey`.
 */
export interface CupLayers {
  a: string
  b: string
  c: string
  s1: string
  s2: string
}

// The mockup's palette: espresso, milk, latte, foam, ube, matcha, strawberry,
// orange, oat, biscoff, hazelnut, chocolate, cinnamon, banana.
const E = '#5A3A2A'
const E2 = '#7A5238'
const M = '#FFF1DC'
const L = '#C9A27E'
const F = '#F6F0FF'
const U = '#8E6BBF'
const G = '#8DB36A'
const G2 = '#B5D197'
const S = '#F2A2B5'
const S2 = '#FAD1DA'
const R = '#EE6F6F'
const O = '#F6B04D'
const OAT = '#F2E6CF'
const BIS = '#C58B4E'
const HZ = '#B98A5E'
const CH = '#4A2C20'
const CIN = '#EAD3B0'
const BAN = '#F5DE8A'

const layers = (a: string, b: string, c: string, s1: string, s2: string): CupLayers => ({ a, b, c, s1, s2 })

/** A milky latte: reads as "a drink" without claiming to be any particular one. */
export const DEFAULT_LAYERS: CupLayers = layers(M, L, M, '30%', '60%')

export const CUP_LAYERS: Record<string, CupLayers> = {
  americano: layers(E, E2, E, '30%', '70%'),
  'cloud americano': layers(F, E, E2, '22%', '64%'),
  'cafe latte': layers(E, L, M, '22%', '46%'),
  'spanish latte': layers(E, L, M, '34%', '52%'),
  'sea salt cream': layers(F, E, L, '24%', '60%'),
  'strawberry bean': layers(E, M, S, '26%', '58%'),
  'dark mocha latte': layers(E, CH, L, '24%', '58%'),
  'dirty matcha': layers(E, G, M, '24%', '62%'),
  'cinnamon cloud': layers(CIN, E, M, '22%', '50%'),
  'hazelnut latte': layers(E, HZ, M, '26%', '54%'),
  'classic ube latte': layers(E, M, U, '26%', '58%'),
  'oat biscoff latte': layers(E, BIS, OAT, '26%', '52%'),
  'ube oat latte': layers(E, OAT, U, '26%', '58%'),
  'strawberry milk': layers(M, M, S, '30%', '62%'),
  'iced choco-hazelnut': layers(M, HZ, CH, '34%', '62%'),
  'dreamy ube milk': layers(F, M, U, '22%', '58%'),
  'oat milky biscoff': layers(OAT, OAT, BIS, '30%', '66%'),
  'strawberry swoon': layers(S2, S, R, '30%', '66%'),
  'sunrise berry': layers(O, O, R, '30%', '58%'),
  'signature oat matcha': layers(G, G2, OAT, '40%', '62%'),
  'sea salt cloud matcha': layers(F, G, OAT, '22%', '64%'),
  'ube cloud matcha': layers(F, G, U, '20%', '60%'),
  'strawberry matcha': layers(G, M, S, '36%', '62%'),
  'banana matcha': layers(G, M, BAN, '36%', '62%'),
  'cloud biscoff matcha': layers(F, G, BIS, '22%', '64%'),
}

/** "Strawberry Matcha (Okumidori)" → "Strawberry Matcha": the drink, whichever blend. */
export function baseName(name: string): string {
  return name.replace(/\s*\([^)]*\)\s*$/, '').trim()
}

/** The lookup key: base name, lower-cased, spaces collapsed. */
export function cupKey(name: string): string {
  return baseName(name).toLowerCase().replace(/\s+/g, ' ')
}

export function cupLayersFor(name: string): CupLayers {
  return CUP_LAYERS[cupKey(name)] ?? DEFAULT_LAYERS
}
