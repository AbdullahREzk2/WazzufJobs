namespace WazzufJobs.BLL.Features.Onboarding.Commands.CompleteOnboarding;

public class CompleteOnboardingCommandHandler(
    IUserRepository userRepository,
    ICandidateProfileRepository profileRepository,
    ICategoryRepository categoryRepository): IRequestHandler<CompleteOnboardingCommand, Result>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;

    public async Task<Result> Handle(CompleteOnboardingCommand request,CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(request.UserId);

        if (user is null)
            return Result.Failure(UserErrors.UserNotFound);

        var profile = await profileRepository.GetAsync(user.Id, cancellationToken);

        // check not already completed
        if (profile?.IsProfileComplete == true)
            return Result.Failure(OnboardingErrors.AlreadyCompleted);

        // validate all category ids exist
        foreach (var categoryId in request.Request.InterestedCategoryIds)
        {
            var exists = await _categoryRepository
                .GetByIdAsync(categoryId, cancellationToken);

            if (exists is null)
                return Result.Failure(OnboardingErrors.InvalidCategory);
        }

        // Phase 02 S2b: onboarding answers are stored in the candidate profile
        if (profile is null)
        {
            profile = new CandidateProfile { UserId = user.Id, IsDemo = user.IsDemo };
            await profileRepository.AddAsync(profile, cancellationToken);
        }

        profile.ExperienceYears = request.Request.ExperienceYears;
        profile.CareerLevel = request.Request.CareerLevel;
        profile.ShowSalary = request.Request.ShowSalary;
        profile.PreferredJobTypes = request.Request.PreferredJobTypes;
        profile.PreferredWorkplaceTypes = request.Request.PreferredWorkplaceTypes;
        profile.InterestedCategoryIds = request.Request.InterestedCategoryIds;
        profile.InterestedJobTitles = request.Request.InterestedJobTitles;
        profile.MinSalary = request.Request.MinSalary;
        profile.IsProfileComplete = true;
        profile.UpdatedAt = DateTime.UtcNow;

        await profileRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}