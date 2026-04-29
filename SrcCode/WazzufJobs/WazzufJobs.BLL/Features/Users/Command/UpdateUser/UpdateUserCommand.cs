namespace WazzufJobs.BLL.Features.Users.Command.UpdateUser;
public record UpdateUserCommand(string userId, UpdateUserRequest userRequest) : IRequest<Result>;
