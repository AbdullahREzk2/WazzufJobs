namespace WazzufJobs.BLL.Contracts.UserCV;
public record CVResponse(
    int Id,
    string Url,
    string FileName,
    DateTime UploadedAt
);
