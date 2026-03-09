using System.Text.Json.Serialization;

namespace MyMascada.Application.Features.BankConnections.DTOs;

/// <summary>
/// Base payload received from Akahu webhooks.
/// </summary>
public record AkahuWebhookPayload
{
    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; init; } = string.Empty;

    [JsonPropertyName("webhook_code")]
    public string WebhookCode { get; init; } = string.Empty;

    /// <summary>
    /// User identifier set during webhook subscription (our user's Guid).
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// The item ID — meaning varies by webhook_type:
    /// TOKEN: user access token, ACCOUNT: account ID, TRANSACTION: account ID.
    /// </summary>
    [JsonPropertyName("item_id")]
    public string? ItemId { get; init; }

    /// <summary>
    /// Fields that were updated (ACCOUNT UPDATE only).
    /// </summary>
    [JsonPropertyName("updated_fields")]
    public string[]? UpdatedFields { get; init; }

    /// <summary>
    /// Count of new transactions (TRANSACTION INITIAL_UPDATE / DEFAULT_UPDATE).
    /// </summary>
    [JsonPropertyName("new_transactions")]
    public int? NewTransactions { get; init; }

    /// <summary>
    /// IDs of new transactions (TRANSACTION INITIAL_UPDATE / DEFAULT_UPDATE).
    /// </summary>
    [JsonPropertyName("new_transaction_ids")]
    public string[]? NewTransactionIds { get; init; }

    /// <summary>
    /// IDs of removed transactions (TRANSACTION DELETE).
    /// </summary>
    [JsonPropertyName("removed_transactions")]
    public string[]? RemovedTransactions { get; init; }
}

/// <summary>
/// Known Akahu webhook types.
/// </summary>
public static class AkahuWebhookTypes
{
    public const string Token = "TOKEN";
    public const string Account = "ACCOUNT";
    public const string Transaction = "TRANSACTION";
}

/// <summary>
/// Known Akahu webhook event codes.
/// </summary>
public static class AkahuWebhookCodes
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string InitialUpdate = "INITIAL_UPDATE";
    public const string DefaultUpdate = "DEFAULT_UPDATE";
}

/// <summary>
/// Represents an Akahu webhook subscription.
/// </summary>
public record AkahuWebhookSubscription
{
    [JsonPropertyName("_id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("webhook_type")]
    public string WebhookType { get; init; } = string.Empty;

    public string? State { get; init; }
}
