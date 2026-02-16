using System.Text.Json.Serialization;

namespace MyMascada.Infrastructure.Services.Telegram.Models;

public class TelegramResponse<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; set; } = default!;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("date")]
    public long Date { get; set; }
}

public class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class TelegramBotUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}
