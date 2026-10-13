using XW.System.Application.Contracts.Dtos.Position;

namespace XW.System.Application.Validators;

/// <summary>校验岗位创建。</summary>
public class PositionCreationDtoValidator : AbstractValidator<PositionCreationDto>
{
    /// <summary>创建校验器。</summary>
    public PositionCreationDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, Position.Name_MaxLength);
        RuleFor(x => x.Code).NotEmpty().Length(2, Position.Code_MaxLength);
    }
}
