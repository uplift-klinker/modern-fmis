using FluentValidation;

namespace Fmis.Core.Clients.CreateClient;

public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(c => c.Email)
            .EmailAddress().WithMessage("Enter a valid email address.")
            .When(c => !string.IsNullOrWhiteSpace(c.Email));

        RuleFor(c => c.PhoneNumber)
            .Must(BeAValidPhoneNumber)
            .WithMessage("Enter a valid phone number with a country code, e.g. +1 555 555 0100.")
            .When(c => !string.IsNullOrWhiteSpace(c.PhoneNumber));

        RuleFor(c => c)
            .Must(HasContactMethod)
            .WithName("contact")
            .WithMessage("Either an email or a phone number is required.");
    }

    private static bool HasContactMethod(CreateClientCommand command)
        => !string.IsNullOrWhiteSpace(command.Email) || !string.IsNullOrWhiteSpace(command.PhoneNumber);

    private static bool BeAValidPhoneNumber(string? phoneNumber)
    {
        var value = (phoneNumber ?? string.Empty).Trim();
        if (!value.StartsWith('+'))
        {
            return false;
        }

        var digitCount = value.Count(char.IsDigit);
        return digitCount is >= 10 and <= 15;
    }
}
