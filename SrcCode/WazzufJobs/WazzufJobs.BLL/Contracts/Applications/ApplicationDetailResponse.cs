namespace WazzufJobs.BLL.Contracts.Applications;
public record ApplicationDetailResponse(
    int Id,
    string ApplicantUserId,
    string ApplicantName,
    string ApplicantEmail,
    string? ApplicantProfilePhotoUrl,
    bool HasCv,
    string JobTitle,
    string Status,
    float? AIScore,
    string? AIFeedback,
    bool IsAIScored,
    DateTime AppliedAt,
    decimal? DesiredSalary,
    bool ShowSalary
);