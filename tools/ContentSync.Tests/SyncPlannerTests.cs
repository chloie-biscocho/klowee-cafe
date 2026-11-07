using Klowee.ContentSync;

namespace ContentSync.Tests;

public class SyncPlannerTests
{
    private static readonly StorageUrls Dev = new("https://devref.supabase.co");
    private static readonly StorageUrls Prod = new("https://prodref.supabase.co");

    [Fact]
    public void Rows_LoseCreatedBy_GetDestinationUrls_AndOrphansAreDropped()
    {
        var eventId = Guid.NewGuid();
        var goneEvent = Guid.NewGuid(); // soft-deleted at source, so not read
        var events = new TableRows(["id", "cover_photo_url", "created_by"], ["uuid", "text", "uuid"],
            [[eventId, Dev.PublicUrl("events/a.webp"), Guid.NewGuid()]]);
        var photos = new TableRows(["id", "event_id", "url", "created_by"], ["uuid", "uuid", "text", "uuid"],
        [
            [Guid.NewGuid(), eventId, "https://example.com/x.jpg", Guid.NewGuid()],
            [Guid.NewGuid(), goneEvent, Dev.PublicUrl("events/b.webp"), null]
        ]);
        var settings = new TableRows(["id", "key", "value", "created_by"], ["uuid", "text", "text", "uuid"],
        [
            [Guid.NewGuid(), "hero_image_url", Dev.PublicUrl("site/h.webp"), null],
            [Guid.NewGuid(), "hero_heading", Dev.PublicUrl("site/not-a-photo.webp"), null]
        ]);

        var order = ContentTables.InsertOrder(["events", "site_settings"]);
        var paths = new SortedSet<string>();
        var result = SyncPlanner.Transform(order,
            new Dictionary<string, TableRows> { ["events"] = events, ["event_photos"] = photos, ["site_settings"] = settings },
            new Dictionary<string, HashSet<Guid>>(), Dev, Prod, paths);

        var copiedEvent = Assert.Single(result["events"].Rows.Rows);
        Assert.Null(copiedEvent[2]);
        Assert.Equal(Prod.PublicUrl("events/a.webp"), copiedEvent[1]);

        // The photo of the soft-deleted event would break the foreign key.
        Assert.Equal(1, result["event_photos"].Orphans);
        Assert.Equal("https://example.com/x.jpg", Assert.Single(result["event_photos"].Rows.Rows)[2]);

        // Only the image settings are URLs; other settings are copied verbatim.
        Assert.Equal(Prod.PublicUrl("site/h.webp"), result["site_settings"].Rows.Rows[0][2]);
        Assert.Equal(Dev.PublicUrl("site/not-a-photo.webp"), result["site_settings"].Rows.Rows[1][2]);

        Assert.Equal(["events/a.webp", "site/h.webp"], paths);

        // The source rows themselves are not modified.
        Assert.NotNull(events.Rows[0][2]);
    }
}
