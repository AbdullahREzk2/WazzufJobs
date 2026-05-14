namespace WazzufJobs.BLL.Features.SavedJobs.Commands.RemoveSavedJob;

public record RemoveSavedJobCommand(string UserId,int JobId) : IRequest<Result>;