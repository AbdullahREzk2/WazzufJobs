using WazzufJobs.BLL.Contracts.Applications;

namespace WazzufJobs.BLL.Features.Applications.Commands.UpdateApplicationStatus;
public record UpdateApplicationStatusCommand(int Id, UpdateApplicationStatusRequest Request) : IRequest<Result>;
