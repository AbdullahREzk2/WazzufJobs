using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WazzufJobs.BLL.Abstractions.Consts;
using WazzufJobs.DAL.Entities;
using WazzufJobs.DAL.Persistence;
using WazzufJobs.Tests.Infrastructure;

namespace WazzufJobs.Tests;

/// <summary>
/// Requests the existing clients send today must stay valid once validators run (Phase 02, S1). Each case below
/// would have been rejected by an unadjusted validator. Relational (profile updates use ExecuteUpdate).
/// </summary>
public class ValidationCompatibilityTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    private async Task<AppUser> CreateUserAsync(string firstName = "Al", string lastName = "Li")
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = TestData.NewUser("compat");
        user.FirstName = firstName;
        user.LastName = lastName;
        Assert.True((await users.CreateAsync(user)).Succeeded);
        return user;
    }

    [SqlServerFact]
    public async Task Profile_update_accepts_names_shorter_than_three_characters()
    {
        var user = await CreateUserAsync();

        var response = await factory.CreateClient().AsCandidate(factory.Services, user)
            .PutAsJsonAsync("/api/Account/update-User-Info", new { firstName = "Bo", lastName = "Wu" });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task Onboarding_without_interested_categories_or_job_titles_is_accepted()
    {
        var user = await CreateUserAsync();

        var response = await factory.CreateClient().AsCandidate(factory.Services, user).PostAsJsonAsync("/api/Onboarding/complete", new
        {
            experienceYears = 0,
            careerLevel = 0,
            preferredJobTypes = new[] { 0 },
            preferredWorkplaceTypes = new[] { 1 },
            interestedCategoryIds = Array.Empty<int>(),
            interestedJobTitles = Array.Empty<string>(),
            minSalary = 0,
            showSalary = false
        });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task Admin_job_without_skills_or_location_and_with_zero_minimum_salary_is_accepted()
    {
        var admin = await CreateUserAsync("Site", "Admin");
        int categoryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDBContext>();
            var category = new JobCategory { Name = "Compat", Slug = $"compat-{Guid.NewGuid():N}" };
            db.JobCategories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        var response = await factory.CreateClient().AsAdmin(factory.Services, admin).PostAsJsonAsync("/api/Jobs", new
        {
            title = "Backend Developer",
            description = "Build APIs",
            location = "",
            skills = Array.Empty<string>(),
            jobType = 0,
            workplaceType = 0,
            categoryId,
            salaryMin = 0,
            salaryMax = 0,
            expiresAt = (DateTime?)null
        });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [SqlServerFact]
    public async Task Registration_accepts_every_special_character_identity_accepts()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/Auth/Register",
            new { email = $"dot-{Guid.NewGuid():N}@example.test", password = "Abcdefg1.", firstName = "Al", lastName = "Li" });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [SqlServerTheory]
    [InlineData("Abcdefg1.")]          // '.' was missing from the old special-character list
    [InlineData("Abcdefg1 ")]          // space
    [InlineData("Abcdefg1/")]
    [InlineData("Abcdéfg1x")]          // non-ASCII letter counts as non-alphanumeric for Identity
    [InlineData("Abcdefg1!")]
    [InlineData("abcdefg1!")]          // no uppercase
    [InlineData("ABCDEFG1!")]          // no lowercase
    [InlineData("Abcdefgh!")]          // no digit
    [InlineData("Abcdefg12")]          // no special character
    [InlineData("Ab1!xyz")]            // too short
    [InlineData("Abcdefg٣!")]          // Arabic-Indic digit is not a digit for Identity
    public async Task Password_rule_accepts_exactly_what_identity_accepts(string password)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var identityAccepts = true;
        foreach (var validator in users.PasswordValidators)
            identityAccepts &= (await validator.ValidateAsync(users, TestData.NewUser("pw"), password)).Succeeded;

        var ruleAccepts = System.Text.RegularExpressions.Regex.IsMatch(password, RegexPatterns.Password);

        Assert.Equal(identityAccepts, ruleAccepts);
    }
}
