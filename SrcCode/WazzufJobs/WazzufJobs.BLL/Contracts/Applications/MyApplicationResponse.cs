namespace WazzufJobs.BLL.Contracts.Applications;
public record MyApplicationResponse(
    int Id,
    string JobTitle,
    string JobLocation,
    string CategoryName,
    string Status,
    float? AIScore,
    string? AIFeedback,
    bool IsAIScored,
    DateTime AppliedAt
);
