using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using WazzufJobs.API.Controller;
using WazzufJobs.BLL.Authentication;
using WazzufJobs.DAL.IRepository;

namespace WazzufJobs.Tests;

/// <summary>
/// Architecture rules of the company module (Phase 02, §4 layer 2): company repositories offer only company-scoped
/// methods, every company handler checks access, and a company id is taken only from the route, never from a body.
/// </summary>
public class CompanyModuleStructureTests
{
    [Fact]
    public void Every_company_repository_method_is_scoped_by_the_company_id()
    {
        var unscoped = typeof(ICompanyRepository).GetMethods()
            .Where(m => m.Name != nameof(ICompanyRepository.GetPlatformCompanyIdAsync))
            .Where(m => m.GetParameters() is not [{ Name: "companyId" } first, ..] || first.ParameterType != typeof(Guid))
            .Select(m => m.Name)
            .ToList();

        Assert.True(unscoped.Count == 0, "Unscoped company repository methods: " + string.Join(", ", unscoped));
        Assert.True(typeof(ICompanyRepository).GetMethods().Length >= 8);
    }

    [Fact]
    public void Every_company_handler_checks_company_access()
    {
        var handlers = typeof(ICompanyAccessService).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("WazzufJobs.BLL.Features.Companies", StringComparison.Ordinal) == true)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
            .ToList();

        Assert.True(handlers.Count >= 4, $"expected the company handlers, found {handlers.Count}");
        foreach (var handler in handlers)
            Assert.True(handler.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(ICompanyAccessService)),
                $"{handler.Name} does not depend on ICompanyAccessService");
    }

    [Fact]
    public void Company_requests_carry_the_company_id_only_from_the_route()
    {
        var actions = typeof(CompaniesController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var route = action.GetCustomAttributes<HttpMethodAttribute>().Single().Template!;
            Assert.StartsWith("{companyId:guid}", route);
            foreach (var parameter in action.GetParameters().Where(p => p.ParameterType != typeof(CancellationToken)))
            {
                Assert.Equal("companyId", parameter.Name);
                Assert.NotNull(parameter.GetCustomAttribute<FromRouteAttribute>());
            }
            Assert.DoesNotContain(action.GetParameters(), p => p.GetCustomAttribute<FromBodyAttribute>() is not null);
        }
    }
}
