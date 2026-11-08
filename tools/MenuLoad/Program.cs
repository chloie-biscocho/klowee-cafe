using Klowee.MenuLoad;

// Loads a menu (docs/menu/*.json) into a Klowee environment through the admin
// API: categories, items (created, renamed, moved, archived), a published
// pop-up version, and the owners' office draft. Dry run by default; --apply
// writes. Idempotent: a second run plans nothing. Handover 12a.

var apply = args.Contains("--apply");
var menuPath = args.SkipWhile(a => a != "--menu").Skip(1).FirstOrDefault() ?? "docs/menu/2026-11-popup-menu.json";

var missing = new[] { "KLOWEE_API_URL", "KLOWEE_OWNER_EMAIL", "KLOWEE_OWNER_PASSWORD" }
    .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine($"Missing environment variable(s): {string.Join(", ", missing)}.");
    return 1;
}

var apiUrl = Environment.GetEnvironmentVariable("KLOWEE_API_URL")!;
try
{
    var menu = MenuFile.Load(menuPath);
    using var api = await Api.SignInAsync(apiUrl, Environment.GetEnvironmentVariable("KLOWEE_OWNER_EMAIL")!,
        Environment.GetEnvironmentVariable("KLOWEE_OWNER_PASSWORD")!);

    Console.WriteLine($"API   {new Uri(apiUrl).Host}");
    Console.WriteLine($"Menu  {menuPath}: {menu.Lines().Count()} items in {menu.Categories.Count} categories");
    Console.WriteLine($"Mode  {(apply ? "APPLY" : "dry run (nothing is written)")}");
    Console.WriteLine();

    var plan = await Planner.PlanAsync(api, menu);
    foreach (var note in plan.Notes)
    {
        Console.WriteLine($"  note     {note}");
    }

    foreach (var step in plan.Steps)
    {
        Console.WriteLine($"  {step.Kind,-8} {step.Description}");
    }

    Console.WriteLine();
    Console.WriteLine(plan.Steps.Count == 0
        ? "Nothing to do: the environment already matches the menu."
        : "Plan: " + string.Join(", ", plan.Steps.GroupBy(s => s.Kind).Select(g => $"{g.Count()} {g.Key}")));

    if (!apply || plan.Steps.Count == 0)
    {
        return 0;
    }

    foreach (var step in plan.Steps)
    {
        await step.Run();
    }

    var current = await api.CurrentAsync("PopUp");
    var categories = (await api.ItemsAsync()).Where(i => current.Items.Any(v => v.MenuItemId == i.Id)).Select(i => i.CategoryName).Distinct().Count();
    Console.WriteLine($"Applied. Current pop-up menu: \"{current.Name}\", {current.Items.Count} items in {categories} categories.");
    return 0;
}
catch (LoadException ex)
{
    Console.Error.WriteLine($"Stopped: {ex.Message}");
    return 1;
}
