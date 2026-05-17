using WazzufJobs.BLL.Contracts.Onboarding;

namespace WazzufJobs.BLL.Features.Onboarding.Commands.CompleteOnboarding;

public record CompleteOnboardingCommand(string UserId,OnboardingRequest Request) : IRequest<Result>;