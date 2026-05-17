using MediatR;
using WazzufJobs.BLL.Abstractions;
using WazzufJobs.BLL.Contracts.Onboarding;
using WazzufJobs.BLL.Errors;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.BLL.Features.Onboarding.Queries.GetOnboardingStatus;

public class GetOnboardingStatusQueryHandler(IUserRepository userRepository): IRequestHandler<GetOnboardingStatusQuery, Result<OnboardingStatusResponse>>
{
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<Result<OnboardingStatusResponse>> Handle(GetOnboardingStatusQuery request,CancellationToken cancellationToken)
    {
        var user = await _userRepository
            .FindByIdWithPreferenceAsync(request.UserId, cancellationToken);

        if (user is null)
            return Result.Failure<OnboardingStatusResponse>(UserErrors.UserNotFound);

        return Result.Success(new OnboardingStatusResponse(
            user.IsProfileComplete,
            user.ExperienceYears,
            user.CareerLevel.ToString(),
            user.Preference?.PreferredJobTypes
                .Select(j => j.ToString()).ToList() ?? [],
            user.Preference?.PreferredWorkplaceTypes
                .Select(w => w.ToString()).ToList() ?? [],
            user.Preference?.InterestedCategoryIds ?? [],
            user.Preference?.InterestedJobTitles ?? [],
            user.Preference?.MinSalary,
            user.ShowSalary));
    }
}