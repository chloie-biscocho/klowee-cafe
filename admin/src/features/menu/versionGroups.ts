import type { MenuContext, MenuVersionSummaryDto } from '../../types/api'

/**
 * Splits the version list into the working set and "historical" versions: an
 * unpublished version whose effective date is older than the current published
 * version of its context (the imported office menus, a superseded pop-up). The
 * current version is the published one with the latest effective date on or
 * before `today`, the same rule the API uses. A context with no current version
 * has nothing historical. `all` is every context's versions, so the rule holds
 * while the list is filtered to one context.
 */
export function splitHistorical(
  shown: MenuVersionSummaryDto[],
  all: MenuVersionSummaryDto[],
  today: string,
): { active: MenuVersionSummaryDto[]; historical: MenuVersionSummaryDto[] } {
  const current = new Map<MenuContext, string>()
  for (const v of all) {
    if (!v.isPublished || v.effectiveFrom > today) continue
    const seen = current.get(v.context)
    if (seen === undefined || v.effectiveFrom > seen) current.set(v.context, v.effectiveFrom)
  }

  const isHistorical = (v: MenuVersionSummaryDto) => {
    const from = current.get(v.context)
    return !v.isPublished && from !== undefined && v.effectiveFrom < from
  }

  return { active: shown.filter((v) => !isHistorical(v)), historical: shown.filter(isHistorical) }
}
