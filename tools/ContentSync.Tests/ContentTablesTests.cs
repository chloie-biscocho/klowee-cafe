using Klowee.ContentSync;

namespace ContentSync.Tests;

public class ContentTablesTests
{
    [Fact]
    public void EveryTable_ComesAfterTheTablesItReferences()
    {
        var order = ContentTables.InsertOrder().Select(t => t.Name).ToList();

        foreach (var table in ContentTables.All)
        {
            foreach (var parent in table.Parents)
            {
                Assert.True(
                    order.IndexOf(parent.ParentTable) < order.IndexOf(table.Name),
                    $"{parent.ParentTable} must be inserted before {table.Name}");
            }
        }
    }

    [Fact]
    public void TheOrder_IsTheOneTheHandoverLocks()
    {
        Assert.Equal(
            ["menu_categories", "menu_items", "add_ons", "menu_versions", "menu_version_items", "packages",
             "package_inclusions", "events", "event_photos", "announcements", "site_settings"],
            ContentTables.InsertOrder().Select(t => t.Name));
    }

    [Fact]
    public void DeleteOrder_IsTheExactReverse() =>
        Assert.Equal(
            ContentTables.InsertOrder().Select(t => t.Name).Reverse(),
            ContentTables.DeleteOrder().Select(t => t.Name));

    [Fact]
    public void UsersAndMigrationHistory_AreNeverContentTables()
    {
        var names = ContentTables.All.Select(t => t.Name).ToList();
        Assert.DoesNotContain("users", names);
        Assert.DoesNotContain("__EFMigrationsHistory", names);
        Assert.Throws<ArgumentException>(() => ContentTables.InsertOrder(["users"]));
    }

    [Fact]
    public void SelectingTables_KeepsDependencyOrder_AndBringsDependentsAlong()
    {
        // Out of order on purpose; menu_items drags menu_version_items along,
        // because deleting menu items would break the version items that point at them.
        var order = ContentTables.InsertOrder(["site_settings", "menu_items"]).Select(t => t.Name);

        Assert.Equal(["menu_items", "menu_version_items", "site_settings"], order);
    }

    [Fact]
    public void SelectingAParent_BringsGrandchildrenToo() =>
        Assert.Equal(
            ["menu_categories", "menu_items", "menu_version_items"],
            ContentTables.InsertOrder(["menu_categories"]).Select(t => t.Name));
}
