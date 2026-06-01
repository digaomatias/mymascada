using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Security;

public class EndpointValidator : IEndpointValidator
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EndpointValidator> _logger;
    private readonly HashSet<string> _allowedHosts;

    public EndpointValidator(
        IHostEnvironment environment,
        ILogger<EndpointValidator> logger,
        IOptions<EndpointValidationOptions> options)
    {
        _environment = environment;
        _logger = logger;
        _allowedHosts = new HashSet<string>(options.Value.AllowedAiProviderHosts, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<EndpointValidationResult> ValidateEndpointAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return EndpointValidationResult.Valid();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return EndpointValidationResult.Invalid("Invalid API endpoint URL format.");

        // Validate scheme first — block non-HTTP(S) schemes (e.g. file://) before any other checks
        var isLocalhost = uri.Host is "localhost" or "127.0.0.1" or "::1";

        if (isLocalhost)
        {
            if (uri.Scheme is not "http" and not "https")
                return EndpointValidationResult.Invalid("API endpoint must use HTTP or HTTPS scheme.");

            if (_environment.IsDevelopment())
                return EndpointValidationResult.Valid();

            return EndpointValidationResult.Invalid(
                "Localhost endpoints are not allowed in production. Use an external AI provider URL.");
        }

        // HTTPS required for non-localhost
        if (uri.Scheme != "https")
            return EndpointValidationResult.Invalid("API endpoint must use HTTPS.");

        // Known AI provider hosts are always allowed (skip DNS resolution)
        if (_allowedHosts.Contains(uri.Host))
            return EndpointValidationResult.Valid();

        // Resolve hostname and check all resolved IPs
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.Host);
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(
                ex,
                "DNS resolution failed for AI endpoint host (SocketErrorCode: {ErrorCode})",
                ex.SocketErrorCode);
            return EndpointValidationResult.Invalid("Could not resolve the API endpoint hostname.");
        }

        if (addresses.Length == 0)
            return EndpointValidationResult.Invalid("Could not resolve the API endpoint hostname.");

        foreach (var ip in addresses)
        {
            if (IsBlockedAddress(ip))
            {
                _logger.LogWarning(
                    "SSRF attempt blocked: endpoint resolved to blocked IP {IP}",
                    ip);
                return EndpointValidationResult.Invalid(
                    "API endpoint resolves to a private or reserved IP address, which is not allowed.");
            }
        }

        return EndpointValidationResult.Valid();
    }

    /// <summary>
    /// Strips control characters and truncates user input for safe log inclusion (prevents log injection).
    /// </summary>
    private static string SanitizeForLog(string input)
        => new(input.Where(c => !char.IsControl(c)).Take(200).ToArray());

    private static bool IsBlockedAddress(IPAddress ip)
    {
        // Loopback: 127.0.0.0/8, ::1
        if (IPAddress.IsLoopback(ip))
            return true;

        // Map IPv6-mapped IPv4 to IPv4 for consistent checking
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10)
                return true;

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;

            // Link-local: 169.254.0.0/16 (includes cloud metadata 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;

            // 0.0.0.0/8
            if (bytes[0] == 0)
                return true;

            // 100.64.0.0/10 (Carrier-grade NAT / shared address space)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                return true;

            // 192.0.0.0/24 (IETF protocol assignments)
            if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
                return true;

            // 198.18.0.0/15 (benchmark testing)
            if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19))
                return true;

            // Broadcast: 255.255.255.255
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
                return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // IPv6 link-local: fe80::/10
            if (ip.IsIPv6LinkLocal)
                return true;

            // IPv6 site-local (deprecated but still block): fec0::/10
            if (ip.IsIPv6SiteLocal)
                return true;

            // IPv6 unique local: fc00::/7
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
                return true;

            // Unspecified address ::
            if (ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any))
                return true;
        }

        return false;
    }
}
