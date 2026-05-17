using WazzufJobs.BLL.Contracts.Onboarding;

namespace WazzufJobs.BLL.Features.Onboarding.Queries.GetOnboardingStatus;

public record GetOnboardingStatusQuery(string UserId): IRequest<Result<OnboardingStatusResponse>>;