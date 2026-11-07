using Klowee.ContentSync;

namespace ContentSync.Tests;

public class StorageUrlsTests
{
    private static readonly StorageUrls Dev = new("https://devref.supabase.co");
    private static readonly StorageUrls Prod = new("https://prodref.supabase.co/");

    [Fact]
    public void ASourceBucketUrl_BecomesTheSamePathInTheDestinationBucket()
    {
        var url = "https://devref.supabase.co/storage/v1/object/public/media/events/2026/10/abc.webp";

        Assert.Equal(
            "https://prodref.supabase.co/storage/v1/object/public/media/events/2026/10/abc.webp",
            StorageUrls.Rewrite(url, Dev, Prod));
        Assert.Equal("events/2026/10/abc.webp", Dev.ObjectPath(url));
    }

    [Theory]
    [InlineData("https://example.com/photo.jpg")]                                                // external
    [InlineData("https://otherref.supabase.co/storage/v1/object/public/media/menu/a.webp")]     // another project
    [InlineData("https://devref.supabase.co/storage/v1/object/public/avatars/a.webp")]          // another bucket
    [InlineData("https://prodref.supabase.co/storage/v1/object/public/media/menu/a.webp")]      // already the destination
    [InlineData("")]
    public void AnythingElse_IsLeftExactlyAsItWas(string url)
    {
        Assert.Equal(url, StorageUrls.Rewrite(url, Dev, Prod));
        Assert.Null(Dev.ObjectPath(url));
    }

    [Fact]
    public void Null_StaysNull() => Assert.Null(StorageUrls.Rewrite(null, Dev, Prod));

    [Fact]
    public void AQueryString_IsNotPartOfTheObjectPath() =>
        Assert.Equal("site/x.webp", Dev.ObjectPath("https://devref.supabase.co/storage/v1/object/public/media/site/x.webp?v=2"));

    [Theory]
    [InlineData("https://devref.supabase.co/rest/v1/")]
    [InlineData("https://devref.supabase.co/storage/v1")]
    [InlineData("https://devref.supabase.co")]
    public void ProjectUrl_ToleratesTheOtherDashboardUrls(string configured) =>
        Assert.Equal("https://devref.supabase.co", new StorageUrls(configured).ProjectUrl);
}
