using WazzufJobs.BLL.Features.Roles.Command.RoleToggleStatus;
using WazzufJobs.DAL.IRepository;

namespace SurveyBasket.BLL.Features.Roles.Command.RoleToggleStatus;
public class RoleToggleStatusCommandHandler(IRoleRepository roleRepository) : IRequestHandler<RoleToggleStatusCommand, Result>
{
    private readonly IRoleRepository _rolerepository = roleRepository;

    public async Task<Result> Handle(RoleToggleStatusCommand request, CancellationToken cancellationToken)
    {

        if (await _rolerepository.getRoleById(request.RoleId) is not { })
            return Result.Failure(RoleErros.RoleNotFound);

        var updatedResult = await _rolerepository.ToggleStatus(request.RoleId);

        if (!updatedResult)
            return Result.Failure(RoleErros.UpdateFailed);

        return Result.Success();
    }
}
