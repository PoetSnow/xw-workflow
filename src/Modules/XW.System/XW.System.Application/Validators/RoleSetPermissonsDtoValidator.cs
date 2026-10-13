using XW.System.Application.Contracts.Dtos.Role;

namespace XW.System.Application.Validators;

/// <summary>
/// Validates <see cref="RoleSetPermissonsDto"/> instances.
/// </summary>
public class RoleSetPermissonsDtoValidator : AbstractValidator<RoleSetPermissonsDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RoleSetPermissonsDtoValidator"/> class.
    /// </summary>
    public RoleSetPermissonsDtoValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0);
        RuleFor(x => x.Permissions).NotNull();
    }
}
