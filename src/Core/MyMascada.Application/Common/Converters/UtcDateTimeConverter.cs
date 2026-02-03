using System.Text.Json;
using System.Text.Json.Serialization;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Common.Converters;

/// <summary>
/// JSON converter that ensures DateTime values are consistently converted to UTC
/// using the same logic as the CSV import process (DateTimeProvider.ToUtc)
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var dateString = reader.GetString();
            if (string.IsNullOrEmpty(dateString))
                return DateTime.MinValue;

            // Try to parse the date string
            if (DateTime.TryParse(dateString, out var parsedDate))
            {
                // Use the same UTC conversion logic as CSV import
                return DateTimeProvider.ToUtc(parsedDate);
            }
            
            throw new JsonException($"Unable to parse date: {dateString}");
        }

        if (reader.TokenType == JsonTokenType.Null)
            return DateTime.MinValue;

        // If it's already a DateTime, ensure it's UTC
        var dateTime = reader.GetDateTime();
        return DateTimeProvider.ToUtc(dateTime);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Ensure the value is UTC before writing
        var utcValue = DateTimeProvider.ToUtc(value);
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}