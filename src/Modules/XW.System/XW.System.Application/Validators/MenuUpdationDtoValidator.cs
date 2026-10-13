using XW.System.Application.Contracts.Dtos.Menu;

namespace XW.System.Application.Validators;

/// <summary>
/// Validates <see cref="MenuUpdationDto"/> instances.
/// </summary>
public class MenuUpdationDtoValidator : AbstractValidator<MenuUpdationDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MenuUpdationDtoValidator"/> class.
    /// </summary>
    public MenuUpdationDtoValidator()
    {
        Include(new MenuCreationDtoValidator());
    }
}
