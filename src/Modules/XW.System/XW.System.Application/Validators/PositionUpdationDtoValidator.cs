using XW.System.Application.Contracts.Dtos.Position;

namespace XW.System.Application.Validators;

/// <summary>校验岗位更新。</summary>
public class PositionUpdationDtoValidator : AbstractValidator<PositionUpdationDto>
{
    /// <summary>创建校验器。</summary>
    public PositionUpdationDtoValidator()
    {
        Include(new PositionCreationDtoValidator());
    }
}
