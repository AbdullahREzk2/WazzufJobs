using FluentValidation;
using FluentValidation.Results;
using MediatR;
using WazzufJobs.BLL.Behaviors;
using WazzufJobs.BLL.Errors;

namespace WazzufJobs.Tests;

/// <summary>Unit tests for the MediatR validation pipeline (Phase 02, S1).</summary>
public class ValidationBehaviorTests
{
    public sealed record ProbeRequest(string? Name, int Age) : IRequest<string>;

    private sealed class NameValidator : AbstractValidator<ProbeRequest>
    {
        public NameValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class AgeAndLengthValidator : AbstractValidator<ProbeRequest>
    {
        public AgeAndLengthValidator()
        {
            RuleFor(x => x.Age).InclusiveBetween(0, 120).WithMessage("Age is out of range.");
            RuleFor(x => x.Name).MaximumLength(5).WithMessage("Name is too long.");
        }
    }

    private static async Task<(string? Response, bool HandlerCalled)> Run(ProbeRequest request, params IValidator<ProbeRequest>[] validators)
    {
        var called = false;
        var behavior = new ValidationBehavior<ProbeRequest, string>(validators);
        var response = await behavior.Handle(request, () => { called = true; return Task.FromResult("handled"); }, CancellationToken.None);
        return (response, called);
    }

    [Fact]
    public async Task Request_without_validators_passes_through_unchanged()
    {
        var (response, called) = await Run(new ProbeRequest(null, -1));

        Assert.True(called);
        Assert.Equal("handled", response);
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var (response, called) = await Run(new ProbeRequest("Ann", 30), new NameValidator(), new AgeAndLengthValidator());

        Assert.True(called);
        Assert.Equal("handled", response);
    }

    [Fact]
    public async Task Invalid_request_never_reaches_the_handler_and_reports_failures_from_every_validator()
    {
        var called = false;
        var behavior = new ValidationBehavior<ProbeRequest, string>([new NameValidator(), new AgeAndLengthValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new ProbeRequest("", 500), () => { called = true; return Task.FromResult("handled"); }, CancellationToken.None));

        Assert.False(called);
        Assert.Equal(["Name is required.", "Age is out of range."], exception.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Multiple_failures_for_one_field_are_all_reported()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            Run(new ProbeRequest("much-too-long", 500), new AgeAndLengthValidator()));

        var errors = ValidationErrors.ToDictionary(exception.Errors);
        Assert.Equal(["Age is out of range."], errors["age"]);
        Assert.Equal(["Name is too long."], errors["name"]);
    }

    [Theory]
    [InlineData("requestDTO.Email", "email")]
    [InlineData("Request.Title", "title")]
    [InlineData("Passrequest.NewPassword", "newPassword")]
    [InlineData("Request.Skills[0]", "skills[0]")]
    [InlineData("userRequest.Roles", "roles")]
    [InlineData("userId", "userId")]
    [InlineData("RoleId", "roleId")]
    [InlineData("", "request")]
    public void Validator_property_paths_map_to_client_field_names(string propertyName, string expected) =>
        Assert.Equal(expected, ValidationErrors.ToClientKey(propertyName));

    [Fact]
    public void Error_map_groups_messages_per_field_and_never_contains_attempted_values()
    {
        var failures = new[]
        {
            new ValidationFailure("requestDTO.Password", "Password is required.", "attempted-secret-value"),
            new ValidationFailure("requestDTO.Password", "Password is too weak.", "attempted-secret-value"),
            new ValidationFailure("requestDTO.Email", "Email is required.", "attempted@example.test")
        };

        var errors = ValidationErrors.ToDictionary(failures);

        Assert.Equal(["Password is required.", "Password is too weak."], errors["password"]);
        Assert.Equal(["Email is required."], errors["email"]);
        Assert.DoesNotContain(errors.Values.SelectMany(v => v), m => m.Contains("attempted"));
    }
}
