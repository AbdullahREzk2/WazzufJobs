namespace WazzufJobs.BLL.Contracts.Companies;

public record CompanyMemberResponse(string UserId, string FirstName, string LastName, string Email, string Role, DateTime JoinedAt);
