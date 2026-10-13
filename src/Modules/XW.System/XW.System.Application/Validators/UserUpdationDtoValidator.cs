using XW.System.Application.Contracts.Dtos.User;
namespace XW.System.Application.Validators;

/// <summary>
/// Validates <see cref="UserUpdationDto"/> instances.
/// </summary>
public class UserUpdationDtoValidator : AbstractValidator<UserUpdationDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserUpdationDtoValidator"/> class.
    /// </summary>
    public UserUpdationDtoValidator()
    {
        Include(new UserCreationAndUpdationDtoValidator());
    }
}
