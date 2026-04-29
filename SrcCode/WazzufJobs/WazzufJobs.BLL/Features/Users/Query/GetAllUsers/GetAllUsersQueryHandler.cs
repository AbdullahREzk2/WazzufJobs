namespace WazzufJobs.BLL.Features.Users.Query.GetAllUsers;
public class GetAllUsersQueryHandler(IUserRepository userRepository) : IRequestHandler<GetAllUsersQuery, IEnumerable<UserResponse>>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<IEnumerable<UserResponse>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userrepository.GetUsersWithRolesAsync(cancellationToken);
        return users.Adapt<IEnumerable<UserResponse>>();
    }
}
