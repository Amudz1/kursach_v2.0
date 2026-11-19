using FluentValidation;

namespace AiAssistant.Application.Auth;

internal static class AccountValidators
{
    public static readonly IValidator<RegisterAccount> Registration = new InlineValidator<RegisterAccount>
    {
        validator => validator.RuleFor(value => value.Username).NotEmpty()
            .Matches(@"^[\p{L}\p{N}_-]{3,30}$").WithMessage("Логин должен содержать 3–30 букв, цифр, дефисов или подчёркиваний."),
        validator => validator.RuleFor(value => value.Email).NotEmpty().MaximumLength(254).EmailAddress()
            .WithMessage("Укажите корректный адрес электронной почты."),
        validator => validator.RuleFor(value => value.Password).NotEmpty().Length(8, 128)
            .WithMessage("Пароль должен содержать от 8 до 128 символов."),
        validator => validator.RuleFor(value => value.PasswordConfirmation).Equal(value => value.Password)
            .WithMessage("Пароли должны совпадать.")
    };

    public static readonly IValidator<ChangeEmail> Email = new InlineValidator<ChangeEmail>
    {
        validator => validator.RuleFor(value => value.Email).NotEmpty().MaximumLength(254).EmailAddress()
            .WithMessage("Укажите корректный адрес электронной почты."),
        validator => validator.RuleFor(value => value.Password).NotEmpty().MaximumLength(128)
    };

    public static readonly IValidator<ChangePassword> Password = new InlineValidator<ChangePassword>
    {
        validator => validator.RuleFor(value => value.CurrentPassword).NotEmpty().MaximumLength(128),
        validator => validator.RuleFor(value => value.Password).NotEmpty().Length(8, 128)
            .WithMessage("Пароль должен содержать от 8 до 128 символов."),
        validator => validator.RuleFor(value => value.PasswordConfirmation).Equal(value => value.Password)
            .WithMessage("Пароли должны совпадать.")
    };
}
