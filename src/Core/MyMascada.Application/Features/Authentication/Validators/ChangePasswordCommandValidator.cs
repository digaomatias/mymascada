using System.Text.RegularExpressions;
using FluentValidation;
using MyMascada.Application.Features.Authentication.Commands;

namespace MyMascada.Application.Features.Authentication.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    private static readonly Regex SpecialCharacterRegex = new(@"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex UpperCaseRegex = new(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowerCaseRegex = new(@"[a-z]", RegexOptions.Compiled);

    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "qwerty", "abc123", "111111", "password123",
        "admin", "letmein", "welcome", "monkey", "dragon", "password1", "123456789",
        "1234567890", "login", "guest", "hello", "test", "master", "root", "user",
        "pass", "default", "access", "secret", "changeme", "temp", "temporary"
    };

    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long")
            .MaximumLength(128)
            .WithMessage("Password cannot be longer than 128 characters")
            .Must(ContainUpperCase)
            .WithMessage("Password must contain at least one uppercase letter")
            .Must(ContainLowerCase)
            .WithMessage("Password must contain at least one lowercase letter")
            .Must(ContainDigit)
            .WithMessage("Password must contain at least one number")
            .Must(ContainSpecialCharacter)
            .WithMessage("Password must contain at least one special character (!@#$%^&*()_+-=[]{}|;':\",./<>?)")
            .Must(NotBeCommonPassword)
            .WithMessage("Password is too common. Please choose a stronger password")
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must be different from current password");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Password confirmation is required")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }

    private static bool ContainUpperCase(string password) =>
        !string.IsNullOrEmpty(password) && UpperCaseRegex.IsMatch(password);

    private static bool ContainLowerCase(string password) =>
        !string.IsNullOrEmpty(password) && LowerCaseRegex.IsMatch(password);

    private static bool ContainDigit(string password) =>
        !string.IsNullOrEmpty(password) && DigitRegex.IsMatch(password);

    private static bool ContainSpecialCharacter(string password) =>
        !string.IsNullOrEmpty(password) && SpecialCharacterRegex.IsMatch(password);

    private static bool NotBeCommonPassword(string password) =>
        string.IsNullOrEmpty(password) || !CommonPasswords.Contains(password);
}
