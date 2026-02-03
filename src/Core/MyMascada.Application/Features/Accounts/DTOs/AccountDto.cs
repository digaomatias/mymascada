using System;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.DTOs;

/// <summary>
/// Basic account information for list views and general operations
/// </summary>
public class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string TypeDisplayName { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? LastFourDigits { get; set; }
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}