import { describe, expect, it } from 'vitest'
import type { MenuVersionSummaryDto } from '../../types/api'
import { splitHistorical } from './versionGroups'

const v = (name: string, context: 'Office' | 'PopUp', effectiveFrom: string, isPublished: boolean): MenuVersionSummaryDto => ({
  id: name, name, context, effectiveFrom, isPublished, notes: null, itemCount: 1,
})

const officeNow = v('Office menu Nov 2026', 'Office', '2026-11-08', true)
const imported = v('Imported office menu (from 2026-03-01)', 'Office', '2026-03-01', false)
const officeDraft = v('Office menu Dec 2026', 'Office', '2026-12-01', false)
const popUpNow = v('Pop-up menu Nov 2026', 'PopUp', '2026-11-08', true)
const oldPopUp = v('April 2026 pop-up', 'PopUp', '2026-04-01', false)
const all = [officeNow, imported, officeDraft, popUpNow, oldPopUp]

describe('splitHistorical', () => {
  it('moves unpublished versions older than their context’s current one, and nothing else', () => {
    const { active, historical } = splitHistorical(all, all, '2026-11-09')
    expect(historical.map((x) => x.name)).toEqual([imported.name, oldPopUp.name])
    expect(active.map((x) => x.name)).toEqual([officeNow.name, officeDraft.name, popUpNow.name])
  })

  it('compares within a context only, even when the list is filtered', () => {
    const { historical } = splitHistorical([popUpNow, oldPopUp], all, '2026-11-09')
    expect(historical).toEqual([oldPopUp])
  })

  it('has nothing historical in a context with no current version (a future publish does not count)', () => {
    const futureOnly = [v('Office Dec', 'Office', '2026-12-01', true), imported]
    expect(splitHistorical(futureOnly, futureOnly, '2026-11-09').historical).toEqual([])
  })

  it('never hides a published version, however old', () => {
    const oldPublished = v('Office menu Jan 2026', 'Office', '2026-01-01', true)
    const list = [...all, oldPublished]
    expect(splitHistorical(list, list, '2026-11-09').active).toContain(oldPublished)
  })
})
