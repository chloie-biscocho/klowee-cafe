using System.Text.Json;

namespace Klowee.MenuLoad;

public record MenuLine(string Name, decimal Price, bool Featured = false);
public record MenuCategory(string Name, string? ItemDescription, List<MenuLine> Items);
public record MenuFile(
    string PopUpVersionName,
    string OfficeDraftName,
    string Source,
    Dictionary<string, List<string>> Renames,
    List<MenuCategory> Categories)
{
    public static MenuFile Load(string path) =>
        JsonSerializer.Deserialize<MenuFile>(File.ReadAllText(path), Api.Json)
        ?? throw new LoadException($"{path} is empty.");

    /// <summary>Every line in menu order, with its category, its description and its overall position.</summary>
    public IEnumerable<(MenuCategory Category, MenuLine Line, int Position)> Lines() =>
        Categories.SelectMany(c => c.Items.Select(i => (c, i))).Select((x, i) => (x.c, x.i, i));
}
