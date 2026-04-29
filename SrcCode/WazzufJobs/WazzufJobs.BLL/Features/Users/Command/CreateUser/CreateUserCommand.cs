namespace WazzufJobs.BLL.Features.Users.Command.CreateUser;
public record CreateUserCommand(CreateUserRequest userRequest) : IRequest<Result<UserResponse>>;
