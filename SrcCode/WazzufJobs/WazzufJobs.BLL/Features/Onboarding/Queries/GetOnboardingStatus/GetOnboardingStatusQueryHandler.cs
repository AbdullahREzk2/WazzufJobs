using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Onboarding;
using WazzufJobs.BLL.Errors;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Onboarding.Queries.GetOnboardingStatus;

public class GetOnboardingStatusQueryHandler(IUserRepository userRepository, ICandidateProfileRepository profileRepository): IRequestHandler<GetOnboardingStatusQuery, Result<OnboardingStatusResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<Result<OnboardingStatusResponse>> Handle(GetOnboardingStatusQuery request,CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindByIdAsync(request.UserId);

        if (user is null)
            return Result.Failure<OnboardingStatusResponse>(UserErrors.UserNotFound);

        // a candidate without a profile has not completed onboarding (same defaults as before)
        var profile = await profileRepository.GetAsync(user.Id, cancellationToken);

        return Result.Success(new OnboardingStatusResponse(
            profile?.IsProfileComplete ?? false,
            profile?.ExperienceYears ?? 0,
            (profile?.CareerLevel ?? default).ToString(),
            profile?.PreferredJobTypes.Select(j => j.ToString()).ToList() ?? [],
            profile?.PreferredWorkplaceTypes.Select(w => w.ToString()).ToList() ?? [],
            profile?.InterestedCategoryIds ?? [],
            profile?.InterestedJobTitles ?? [],
            profile?.MinSalary,
            profile?.ShowSalary ?? false));
    }
}