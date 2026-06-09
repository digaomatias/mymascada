using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyMascada.Infrastructure.Services.Push;

/// <summary>
/// FCM client backed by the FirebaseAdmin SDK.
///
/// Credential resolution (first match wins):
/// 1. <c>Firebase:ServiceAccountJson</c> configuration value
///    (env var <c>Firebase__ServiceAccountJson</c>) — either the raw service
///    account JSON or a path to the JSON file.
/// 2. <c>GOOGLE_APPLICATION_CREDENTIALS</c> env var pointing to the JSON file
///    (standard Google application-default credentials).
///
/// When neither is configured the client is fail-soft: <see cref="IsConfigured"/>
/// is false, a warning is logged once, and sends are skipped so development
/// environments work without Firebase.
/// </summary>
public class FirebaseFcmClient : IFcmClient
{
    private readonly ILogger<FirebaseFcmClient> _logger;
    private readonly Lazy<FirebaseMessaging?> _messaging;

    public FirebaseFcmClient(IConfiguration configuration, ILogger<FirebaseFcmClient> logger)
    {
        _logger = logger;
        _messaging = new Lazy<FirebaseMessaging?>(
            () => InitializeMessaging(configuration),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool IsConfigured => _messaging.Value != null;

    public async Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var messaging = _messaging.Value;
        if (messaging == null || tokens.Count == 0)
            return Array.Empty<FcmSendResult>();

        var message = new MulticastMessage
        {
            Tokens = tokens.ToList(),
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data.ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        var batchResponse = await messaging.SendEachForMulticastAsync(message, cancellationToken);

        var results = new List<FcmSendResult>(tokens.Count);
        for (var i = 0; i < tokens.Count; i++)
        {
            var response = batchResponse.Responses[i];
            if (response.IsSuccess)
            {
                results.Add(new FcmSendResult { Token = tokens[i], Success = true });
                continue;
            }

            var messagingError = (response.Exception as FirebaseMessagingException)?.MessagingErrorCode;
            var isTokenInvalid = messagingError is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument;
            results.Add(new FcmSendResult
            {
                Token = tokens[i],
                Success = false,
                IsTokenInvalid = isTokenInvalid,
                Error = response.Exception?.Message
            });
        }

        return results;
    }

    private FirebaseMessaging? InitializeMessaging(IConfiguration configuration)
    {
        try
        {
            var credential = ResolveCredential(configuration);
            if (credential == null)
            {
                _logger.LogWarning(
                    "Firebase push notifications are not configured. Set Firebase__ServiceAccountJson " +
                    "(raw service account JSON or a file path) or GOOGLE_APPLICATION_CREDENTIALS to enable push delivery.");
                return null;
            }

            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = credential
            });

            _logger.LogInformation("Firebase push notification client initialized");
            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Firebase push notification client. Push delivery is disabled");
            return null;
        }
    }

    private static GoogleCredential? ResolveCredential(IConfiguration configuration)
    {
        var serviceAccountJson = configuration["Firebase:ServiceAccountJson"];
        if (!string.IsNullOrWhiteSpace(serviceAccountJson))
        {
            var trimmed = serviceAccountJson.Trim();
            return trimmed.StartsWith('{')
                ? GoogleCredential.FromJson(trimmed)
                : GoogleCredential.FromFile(trimmed);
        }

        var defaultCredentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (!string.IsNullOrWhiteSpace(defaultCredentialsPath) && File.Exists(defaultCredentialsPath))
        {
            return GoogleCredential.FromFile(defaultCredentialsPath);
        }

        return null;
    }
}
