using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Resolves runtime authentication modes for bank providers based on deployment configuration.
/// </summary>
public interface IBankProviderModeResolver
{
    BankProviderModeResolution Resolve(string providerId);
}

public record BankProviderModeResolution(
    string ProviderId,
    string DefaultMode,
    IReadOnlyList<BankProviderAuthModeInfo> SupportedModes
);
