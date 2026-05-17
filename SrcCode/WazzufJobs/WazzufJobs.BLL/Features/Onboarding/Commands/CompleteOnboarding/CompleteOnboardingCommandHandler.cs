namespace WazzufJobs.BLL.Features.Onboarding.Commands.CompleteOnboarding;

public class CompleteOnboardingCommandHandler(
    IUserRepository userRepository,
    ICategoryRepository categoryRepository): IRequestHandler<CompleteOnboardingCommand, Result>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<Result> Handle(CompleteOnboardingCommand request,CancellationToken cancellationToken)
    {
        var user = await _userRepository
            .FindByIdWithPreferenceAsync(request.UserId, cancellationToken);

        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        // check not already completed
        if (user.IsProfileComplete)
            return Result.Failure(OnboardingErrors.AlreadyCompleted);

        // validate all category ids exist
        foreach (var categoryId in request.Request.InterestedCategoryIds)
        {
            var exists = await _categoryRepository
                .GetByIdAsync(categoryId, cancellationToken);

            if (exists is null)
                return Result.Failure(OnboardingErrors.InvalidCategory);
        }

        // update user profile fields
        user.ExperienceYears = request.Request.ExperienceYears;
        user.CareerLevel = request.Request.CareerLevel;
        user.ShowSalary = request.Request.ShowSalary;
        user.IsProfileComplete = true;

        // create or update preference
        if (user.Preference is null)
        {
            user.Preference = new UserPreference
            {
                UserId = user.Id,
                PreferredJobTypes = request.Request.PreferredJobTypes,
                PreferredWorkplaceTypes = request.Request.PreferredWorkplaceTypes,
                InterestedCategoryIds = request.Request.InterestedCategoryIds,
                InterestedJobTitles = request.Request.InterestedJobTitles,
                MinSalary = request.Request.MinSalary
            };
        }
        else
        {
            user.Preference.PreferredJobTypes = request.Request.PreferredJobTypes;
            user.Preference.PreferredWorkplaceTypes = request.Request.PreferredWorkplaceTypes;
            user.Preference.InterestedCategoryIds = request.Request.InterestedCategoryIds;
            user.Preference.InterestedJobTitles = request.Request.InterestedJobTitles;
            user.Preference.MinSalary = request.Request.MinSalary;
        }

        await _userRepository.UpdateAsync(user);

        return Result.Success();
    }
}