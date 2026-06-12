using FluentValidation;

namespace Fmis.Core.Clients.CreateClient;

public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(c => c)
            .Must(HasContactMethod)
            .WithName("contact")
            .WithMessage("Either an email or a phone number is required.");
    }

    private static bool HasContactMethod(CreateClientCommand command)
        => !string.IsNullOrWhiteSpace(command.Email) || !string.IsNullOrWhiteSpace(command.PhoneNumber);
}
