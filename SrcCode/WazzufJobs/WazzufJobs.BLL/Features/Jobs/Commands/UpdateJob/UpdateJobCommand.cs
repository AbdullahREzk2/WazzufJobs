using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Jobs;

namespace WazzufJobs.BLL.Features.Jobs.Commands.UpdateJob;

public record UpdateJobCommand(int Id, JobRequest Request) : IRequest<Result>;