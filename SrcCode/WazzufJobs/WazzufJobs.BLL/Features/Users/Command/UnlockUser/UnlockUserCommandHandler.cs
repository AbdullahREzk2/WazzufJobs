namespace SurveyBasket.BLL.Features.Users.Command.UnlockUser;
public class UnlockUserCommandHandler(IUserRepository userRepository, IAuditLog audit) : IRequestHandler<UnlockUserCommand, Result>
{
    private readonly IUserRepository _userrepository = userRepository;

    public async Task<Result> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userrepository.FindByIdAsync(request.userId);
        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        // the unlock and its audit entry are committed together
        return await audit.RecordAsync(async () =>
            {
                var result = await _userrepository.SetLockoutEndDateAsync(user, null);
                if (result.Succeeded)
                    return Result.Success();

                var error = result.Errors.First();
                return Result.Failure(new Error(error.Code, error.Description, StatusCodes.Status400BadRequest));
            },
            r => r.IsSuccess ? AuditEvent.Success(AuditActions.UserUnlocked).On(AuditTargets.User, user.Id) : null,
            cancellationToken);
    }
}
