namespace Klowee.MenuLoad;

public record Step(string Kind, string Description, Func<Task> Run);

public record Plan(List<Step> Steps, List<string> Notes);

/// <summary>
/// Compares the environment with the menu file and lists the API calls that
/// would make them match. Names match case-insensitively; an item can also be
/// found under a former name (the file's "renames"). Ids of things created
/// earlier in the plan are looked up when each step runs.
/// </summary>
public static class Planner
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public static async Task<Plan> PlanAsync(Api api, MenuFile menu)
    {
        var steps = new List<Step>();
        var notes = new List<string>();
        var categories = await api.CategoriesAsync();
        var items = await api.ItemsAsync();
        var versions = await api.VersionsAsync();

        // ---- Categories: in the file's order, sort_order 1..n ----
        var categoryIds = new Dictionary<string, Guid>(Ci);
        foreach (var (category, index) in menu.Categories.Select((c, i) => (c, i + 1)))
        {
            var existing = categories.FirstOrDefault(c => Ci.Equals(c.Name, category.Name));
            if (existing is null)
            {
                steps.Add(new Step("create", $"category \"{category.Name}\" (sort {index})", async () =>
                    categoryIds[category.Name] = (await api.CreateCategoryAsync(category.Name, index)).Id));
                continue;
            }

            categoryIds[category.Name] = existing.Id;
            if (existing.Name != category.Name || existing.SortOrder != index)
            {
                steps.Add(new Step("update", $"category \"{existing.Name}\" → \"{category.Name}\", sort {existing.SortOrder} → {index}",
                    () => api.UpdateCategoryAsync(existing.Id, category.Name, index, existing.Description)));
            }
        }

        // ---- Items ----
        var itemIds = new Dictionary<string, Guid>(Ci);
        var matched = new HashSet<Guid>();
        foreach (var (category, line, _) in menu.Lines())
        {
            var formerNames = menu.Renames.GetValueOrDefault(line.Name) ?? [];
            var existing = items.FirstOrDefault(i => Ci.Equals(i.Name, line.Name))
                ?? items.FirstOrDefault(i => formerNames.Contains(i.Name, Ci));
            var description = category.ItemDescription ?? string.Empty;

            if (existing is null)
            {
                steps.Add(new Step("create", $"item \"{line.Name}\" in {category.Name}", async () =>
                    itemIds[line.Name] = (await api.CreateItemAsync(line.Name, description, categoryIds[category.Name])).Id));
                continue;
            }

            matched.Add(existing.Id);
            itemIds[line.Name] = existing.Id;

            var changes = new List<string>();
            if (existing.Name != line.Name) changes.Add($"rename \"{existing.Name}\" → \"{line.Name}\"");
            if (!Ci.Equals(existing.CategoryName, category.Name)) changes.Add($"move {existing.CategoryName} → {category.Name}");
            if ((existing.Description ?? string.Empty) != description) changes.Add("description");
            if (changes.Count > 0)
            {
                steps.Add(new Step(changes[0].StartsWith("rename") ? "rename" : "update", $"item \"{existing.Name}\": {string.Join(", ", changes)}",
                    () => api.UpdateItemAsync(existing.Id, line.Name, description, categoryIds[category.Name], existing.PhotoUrl)));
            }

            if (existing.IsArchived)
            {
                steps.Add(new Step("restore", $"item \"{line.Name}\" (unarchive)", () => api.SetArchivedAsync(existing.Id, false)));
            }
        }

        foreach (var item in items.Where(i => !i.IsArchived && !matched.Contains(i.Id)))
        {
            steps.Add(new Step("archive", $"item \"{item.Name}\" ({item.CategoryName}): not on this menu", () => api.SetArchivedAsync(item.Id, true)));
        }

        List<PricedLine> Desired() => menu.Lines()
            .Select(x => new PricedLine(itemIds[x.Line.Name], x.Line.Price, true, x.Line.Featured, x.Position))
            .ToList();
        var allItemsKnown = menu.Lines().All(x => itemIds.ContainsKey(x.Line.Name));

        // ---- The pop-up version: a draft is filled, then published ----
        var popUp = versions.FirstOrDefault(v => v.Context == "PopUp" && Ci.Equals(v.Name, menu.PopUpVersionName));
        var lineCount = menu.Lines().Count();
        if (popUp is null)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)); // Manila, fixed +08:00
            Guid created = default;
            steps.Add(new Step("version", $"create PopUp draft \"{menu.PopUpVersionName}\" effective {today:yyyy-MM-dd}",
                async () => created = (await api.CreateVersionAsync(menu.PopUpVersionName, "PopUp", today)).Id));
            steps.Add(new Step("version", $"set its {lineCount} priced items", () => api.ReplaceItemsAsync(created, Desired())));
            steps.Add(new Step("version", $"publish \"{menu.PopUpVersionName}\"", () => api.SetPublishedAsync(created, true)));
        }
        else
        {
            var detail = await api.VersionAsync(popUp.Id);
            var same = allItemsKnown && Same(detail.Items, Desired());
            if (!same)
            {
                if (popUp.IsPublished)
                {
                    steps.Add(new Step("version", $"unpublish \"{popUp.Name}\" to change its items", () => api.SetPublishedAsync(popUp.Id, false)));
                }

                steps.Add(new Step("version", $"set \"{popUp.Name}\" to {lineCount} priced items", () => api.ReplaceItemsAsync(popUp.Id, Desired())));
            }

            if (!same || !popUp.IsPublished)
            {
                steps.Add(new Step("version", $"publish \"{popUp.Name}\"", () => api.SetPublishedAsync(popUp.Id, true)));
            }
        }

        // Older pop-up menus stop being published; kept, since orders may reference them.
        foreach (var old in versions.Where(v => v.Context == "PopUp" && v.IsPublished && !Ci.Equals(v.Name, menu.PopUpVersionName)))
        {
            steps.Add(new Step("version", $"unpublish old PopUp \"{old.Name}\" (kept)", () => api.SetPublishedAsync(old.Id, false)));
        }

        // ---- The owners' office draft: filled, left as a draft ----
        var office = versions.FirstOrDefault(v => v.Context == "Office" && Ci.Equals(v.Name, menu.OfficeDraftName));
        if (office is null)
        {
            notes.Add($"Office version \"{menu.OfficeDraftName}\" not found: skipped. Create it as a draft in the admin and run again.");
        }
        else if (office.IsPublished)
        {
            notes.Add($"Office version \"{office.Name}\" is already published: left alone.");
        }
        else
        {
            var detail = await api.VersionAsync(office.Id);
            if (!(allItemsKnown && Same(detail.Items, Desired())))
            {
                steps.Add(new Step("version", $"fill Office draft \"{office.Name}\" with {lineCount} items at pop-up prices (stays a draft)",
                    () => api.ReplaceItemsAsync(office.Id, Desired())));
            }
        }

        return new Plan(steps, notes);
    }

    private static bool Same(List<VersionItem> current, List<PricedLine> desired) =>
        current.Count == desired.Count
        && desired.All(d => current.Any(c => c.MenuItemId == d.MenuItemId && c.Price == d.Price
            && c.IsAvailable == d.IsAvailable && c.IsFeatured == d.IsFeatured && c.SortOrder == d.SortOrder));
}
