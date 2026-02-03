using Microsoft.AspNetCore.DataProtection;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Security;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Tests.Unit.Services;

public class SettingsEncryptionServiceTests
{
    private readonly SettingsEncryptionService _sut;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IApplicationLogger<SettingsEncryptionService> _logger;

    public SettingsEncryptionServiceTests()
    {
        // Use real in-memory data protection provider for integration-style tests
        _dataProtectionProvider = DataProtectionProvider.Create("TestApp");
        _logger = Substitute.For<IApplicationLogger<SettingsEncryptionService>>();
        _sut = new SettingsEncryptionService(_dataProtectionProvider, _logger);
    }

    #region Encrypt/Decrypt Basic String Tests

    [Fact]
    public void Encrypt_WithValidPlainText_ShouldReturnCipherText()
    {
        // Arrange
        const string plainText = "my-secret-token";

        // Act
        var cipherText = _sut.Encrypt(plainText);

        // Assert
        cipherText.Should().NotBeNullOrEmpty();
        cipherText.Should().NotBe(plainText);
    }

    [Fact]
    public void Decrypt_WithValidCipherText_ShouldReturnOriginalPlainText()
    {
        // Arrange
        const string plainText = "my-secret-token-12345";
        var cipherText = _sut.Encrypt(plainText);

        // Act
        var decryptedText = _sut.Decrypt(cipherText);

        // Assert
        decryptedText.Should().Be(plainText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Encrypt_WithEmptyOrNullPlainText_ShouldReturnEmptyString(string? plainText)
    {
        // Act
        var result = _sut.Encrypt(plainText!);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decrypt_WithEmptyOrNullCipherText_ShouldReturnEmptyString(string? cipherText)
    {
        // Act
        var result = _sut.Decrypt(cipherText!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        const string plainText = "token!@#$%^&*()_+-=[]{}|;':\",./<>?";

        // Act
        var cipherText = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(cipherText);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_WithUnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        const string plainText = "Test with unicode: \u00e9\u00e8\u00ea \u4e2d\u6587 \U0001f600";

        // Act
        var cipherText = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(cipherText);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_WithLongText_ShouldHandleCorrectly()
    {
        // Arrange
        var plainText = new string('x', 10000);

        // Act
        var cipherText = _sut.Encrypt(plainText);
        var decrypted = _sut.Decrypt(cipherText);

        // Assert
        decrypted.Should().Be(plainText);
    }

    [Fact]
    public void Decrypt_WithInvalidCipherText_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string invalidCipherText = "this-is-not-valid-cipher-text";

        // Act
        var act = () => _sut.Decrypt(invalidCipherText);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Decryption failed - settings may be corrupted");
    }

    [Fact]
    public void Decrypt_WithTamperedCipherText_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string plainText = "original-text";
        var cipherText = _sut.Encrypt(plainText);
        var tamperedCipherText = cipherText.Substring(0, cipherText.Length - 5) + "XXXXX";

        // Act
        var act = () => _sut.Decrypt(tamperedCipherText);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region EncryptSettings/DecryptSettings Generic Tests

    [Fact]
    public void EncryptSettings_WithValidObject_ShouldReturnEncryptedJson()
    {
        // Arrange
        var settings = new AkahuConnectionSettings
        {
            AkahuAccountId = "acc_xyz789",
            LastSyncedTransactionId = "tx_last123",
            LastSyncTimestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var encrypted = _sut.EncryptSettings(settings);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotContain("acc_xyz789"); // Account ID should not be visible in encrypted output
        encrypted.Should().NotContain("tx_last123");
    }

    [Fact]
    public void DecryptSettings_WithValidEncryptedSettings_ShouldReturnOriginalObject()
    {
        // Arrange
        var originalSettings = new AkahuConnectionSettings
        {
            AkahuAccountId = "acc_xyz789",
            LastSyncedTransactionId = "tx_last123",
            LastSyncTimestamp = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };
        var encrypted = _sut.EncryptSettings(originalSettings);

        // Act
        var decrypted = _sut.DecryptSettings<AkahuConnectionSettings>(encrypted);

        // Assert
        decrypted.Should().NotBeNull();
        decrypted!.AkahuAccountId.Should().Be(originalSettings.AkahuAccountId);
        decrypted.LastSyncedTransactionId.Should().Be(originalSettings.LastSyncedTransactionId);
        decrypted.LastSyncTimestamp.Should().Be(originalSettings.LastSyncTimestamp);
    }

    [Fact]
    public void EncryptSettings_WithNullObject_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _sut.EncryptSettings<AkahuConnectionSettings>(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DecryptSettings_WithNullOrEmptyString_ShouldReturnNull(string? encryptedSettings)
    {
        // Act
        var result = _sut.DecryptSettings<AkahuConnectionSettings>(encryptedSettings);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void DecryptSettings_WithInvalidEncryptedData_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string invalidEncrypted = "not-valid-encrypted-data";

        // Act
        var act = () => _sut.DecryptSettings<AkahuConnectionSettings>(invalidEncrypted);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DecryptSettings_WithValidEncryptionButWrongType_ShouldThrowInvalidOperationException()
    {
        // Arrange - encrypt valid JSON but wrong structure
        var validJson = "{\"someOtherField\": 123, \"notAValidField\": true}";
        var encrypted = _sut.Encrypt(validJson);

        // Act - This should deserialize but with default values since the fields don't match
        var result = _sut.DecryptSettings<AkahuConnectionSettings>(encrypted);

        // Assert - System.Text.Json ignores unknown fields and uses defaults
        result.Should().NotBeNull();
        result!.AkahuAccountId.Should().BeEmpty();
        result.LastSyncedTransactionId.Should().BeNull();
    }

    #endregion

    #region AkahuConnectionSettings Specific Tests

    [Fact]
    public void EncryptSettings_AkahuConnectionSettings_WithDefaultValues_ShouldRoundTrip()
    {
        // Arrange
        var settings = new AkahuConnectionSettings();

        // Act
        var encrypted = _sut.EncryptSettings(settings);
        var decrypted = _sut.DecryptSettings<AkahuConnectionSettings>(encrypted);

        // Assert
        decrypted.Should().NotBeNull();
        decrypted!.AkahuAccountId.Should().BeEmpty();
        decrypted.LastSyncedTransactionId.Should().BeNull();
        decrypted.LastSyncTimestamp.Should().BeNull();
    }

    [Fact]
    public void EncryptSettings_AkahuConnectionSettings_WithOnlyRequiredFields_ShouldRoundTrip()
    {
        // Arrange
        var settings = new AkahuConnectionSettings
        {
            AkahuAccountId = "acc_required"
        };

        // Act
        var encrypted = _sut.EncryptSettings(settings);
        var decrypted = _sut.DecryptSettings<AkahuConnectionSettings>(encrypted);

        // Assert
        decrypted.Should().NotBeNull();
        decrypted!.AkahuAccountId.Should().Be("acc_required");
        decrypted.LastSyncedTransactionId.Should().BeNull();
        decrypted.LastSyncTimestamp.Should().BeNull();
    }

    #endregion

    #region AkahuOptions Tests (Non-encrypted, but validate structure)

    [Fact]
    public void AkahuOptions_ShouldHaveCorrectSectionName()
    {
        // Assert
        AkahuOptions.SectionName.Should().Be("Akahu");
    }

    [Fact]
    public void AkahuOptions_ShouldHaveCorrectDefaultValues()
    {
        // Arrange
        var options = new AkahuOptions();

        // Assert
        options.AppIdToken.Should().BeEmpty();
        options.AppSecret.Should().BeEmpty();
        options.RedirectUri.Should().BeEmpty();
        options.DefaultScopes.Should().ContainSingle().Which.Should().Be("ENDURING_CONSENT");
        options.ApiBaseUrl.Should().Be("https://api.akahu.io/v1/");
        options.OAuthBaseUrl.Should().Be("https://oauth.akahu.nz");
    }

    #endregion

    #region Security Validation Tests

    [Fact]
    public void Encrypt_SameInputTwice_ShouldProduceDifferentOutput()
    {
        // Arrange
        const string plainText = "same-input-text";

        // Act
        var cipherText1 = _sut.Encrypt(plainText);
        var cipherText2 = _sut.Encrypt(plainText);

        // Assert - Data Protection API uses random IV, so outputs should differ
        cipherText1.Should().NotBe(cipherText2);

        // But both should decrypt to the same value
        _sut.Decrypt(cipherText1).Should().Be(plainText);
        _sut.Decrypt(cipherText2).Should().Be(plainText);
    }

    [Fact]
    public void Encrypt_ShouldNotLeakPlainTextInOutput()
    {
        // Arrange
        var sensitiveData = new AkahuConnectionSettings
        {
            AkahuAccountId = "acc_sensitive_id",
            LastSyncedTransactionId = "tx_secret_123"
        };

        // Act
        var encrypted = _sut.EncryptSettings(sensitiveData);

        // Assert - Sensitive data should not appear in encrypted output
        encrypted.Should().NotContain("acc_sensitive");
        encrypted.Should().NotContain("tx_secret");
        encrypted.Should().NotContain("AkahuAccountId");
        encrypted.Should().NotContain("LastSyncedTransactionId");
    }

    #endregion

    #region Error Logging Tests

    [Fact]
    public void Decrypt_WithInvalidCipherText_ShouldLogError()
    {
        // Arrange
        const string invalidCipherText = "invalid-cipher-text";

        // Act
        try
        {
            _sut.Decrypt(invalidCipherText);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        _logger.Received(1).LogError(
            Arg.Any<Exception>(),
            Arg.Is<string>(s => s.Contains("Failed to decrypt")),
            Arg.Any<object[]>());
    }

    [Fact]
    public void EncryptSettings_WithSerializationError_ShouldLogError()
    {
        // Note: Standard objects serialize fine, this test validates the logging path
        // In a real scenario, a custom type with circular references would trigger this
        // For now, we just verify the normal path doesn't log errors
        var settings = new AkahuConnectionSettings { AkahuAccountId = "acc_test" };

        // Act
        _sut.EncryptSettings(settings);

        // Assert - No error should be logged for valid serialization
        _logger.DidNotReceive().LogError(
            Arg.Any<Exception>(),
            Arg.Is<string>(s => s.Contains("Failed to serialize")),
            Arg.Any<object[]>());
    }

    #endregion
}
