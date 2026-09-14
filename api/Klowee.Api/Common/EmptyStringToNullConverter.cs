using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klowee.Api.Common;

/// <summary>
/// Turns an incoming <c>""</c> or whitespace-only string into <c>null</c>, for
/// every string on every request body. An empty text box means "not set", and
/// the API should not have to re-learn that per property. See
/// docs/decisions/008-empty-strings-are-null.md.
/// </summary>
/// <remarks>
/// Registered globally, so it also sees dictionary keys — `ProblemDetails`
/// carries <c>errors</c> and <c>extensions</c> as string-keyed dictionaries.
/// The property-name overrides below pass those through untouched; without
/// them, System.Text.Json throws when it needs a key converter.
/// </remarks>
public class EmptyStringToNullConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // Writing is unchanged: this converter exists to normalise input.
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    public override string ReadAsPropertyName(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.GetString()!;

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options) =>
        writer.WritePropertyName(value);
}
