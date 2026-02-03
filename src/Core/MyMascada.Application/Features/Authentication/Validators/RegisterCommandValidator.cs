using FluentValidation;
using MyMascada.Application.Features.Authentication.Commands;
using System.Text.RegularExpressions;

namespace MyMascada.Application.Features.Authentication.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
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

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Please provide a valid email address")
            .MaximumLength(254)
            .WithMessage("Email address is too long");

        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("Username is required")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters long")
            .MaximumLength(50)
            .WithMessage("Username cannot be longer than 50 characters")
            .Matches(@"^[a-zA-Z0-9_.-]+$")
            .WithMessage("Username can only contain letters, numbers, dots, dashes, and underscores");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
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
            .Must((command, password) => !ContainsPersonalInfo(password, command))
            .WithMessage("Password cannot contain your personal information (name, email, username)");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Password confirmation is required")
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(50)
            .WithMessage("First name cannot be longer than 50 characters")
            .Matches(@"^[a-zA-Z\s'-]+$")
            .WithMessage("First name can only contain letters, spaces, apostrophes, and hyphens");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(50)
            .WithMessage("Last name cannot be longer than 50 characters")
            .Matches(@"^[a-zA-Z\s'-]+$")
            .WithMessage("Last name can only contain letters, spaces, apostrophes, and hyphens");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$")
            .WithMessage("Please provide a valid phone number")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required")
            .Length(3)
            .WithMessage("Currency must be a 3-letter ISO code (e.g., USD, EUR, GBP)")
            .Matches(@"^[A-Z]{3}$")
            .WithMessage("Currency must be uppercase letters only");

        RuleFor(x => x.TimeZone)
            .NotEmpty()
            .WithMessage("Time zone is required")
            .Must(BeValidTimeZone)
            .WithMessage("Please provide a valid time zone identifier");
    }

    private static bool ContainUpperCase(string password)
    {
        return UpperCaseRegex.IsMatch(password);
    }

    private static bool ContainLowerCase(string password)
    {
        return LowerCaseRegex.IsMatch(password);
    }

    private static bool ContainDigit(string password)
    {
        return DigitRegex.IsMatch(password);
    }

    private static bool ContainSpecialCharacter(string password)
    {
        return SpecialCharacterRegex.IsMatch(password);
    }

    private static bool NotBeCommonPassword(string password)
    {
        return !CommonPasswords.Contains(password);
    }

    private static bool ContainsPersonalInfo(string password, RegisterCommand command)
    {
        var lowerPassword = password.ToLowerInvariant();
        var lowerEmail = command.Email.ToLowerInvariant();
        var lowerUserName = command.UserName.ToLowerInvariant();
        var lowerFirstName = command.FirstName.ToLowerInvariant();
        var lowerLastName = command.LastName.ToLowerInvariant();

        // Check if password contains parts of personal information
        return lowerPassword.Contains(lowerFirstName, StringComparison.OrdinalIgnoreCase) ||
               lowerPassword.Contains(lowerLastName, StringComparison.OrdinalIgnoreCase) ||
               lowerPassword.Contains(lowerUserName, StringComparison.OrdinalIgnoreCase) ||
               lowerPassword.Contains(lowerEmail.Split('@')[0], StringComparison.OrdinalIgnoreCase);
    }

    private static bool BeValidTimeZone(string timeZone)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch
        {
            // Also accept UTC as a valid timezone
            return timeZone.Equals("UTC", StringComparison.OrdinalIgnoreCase);
        }
    }
}