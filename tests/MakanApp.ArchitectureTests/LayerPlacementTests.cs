using MakanApp.Api.Controllers;
using MakanApp.Application.Common;
using MakanApp.Domain.Common;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.ArchitectureTests;

public sealed class LayerPlacementTests
{
    [Fact]
    public void ControllersMustExistOnlyInApi()
    {
        var apiAssembly = typeof(AuthController).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        Assert.Contains(typeof(AuthController), controllerTypes);
        Assert.Contains(typeof(ProfileController), controllerTypes);

        var innerAssemblies = new[]
        {
            typeof(DomainAssembly).Assembly,
            typeof(ApplicationAssembly).Assembly,
            typeof(MakanDbContext).Assembly
        };

        Assert.DoesNotContain(
            innerAssemblies.SelectMany(assembly => assembly.GetTypes()),
            type => typeof(ControllerBase).IsAssignableFrom(type));
    }

    [Fact]
    public void DbContextMustExistOnlyInInfrastructure()
    {
        Assert.True(typeof(DbContext).IsAssignableFrom(typeof(MakanDbContext)));

        var nonInfrastructureAssemblies = new[]
        {
            typeof(DomainAssembly).Assembly,
            typeof(ApplicationAssembly).Assembly,
            typeof(AuthController).Assembly
        };

        Assert.DoesNotContain(
            nonInfrastructureAssemblies.SelectMany(assembly => assembly.GetTypes()),
            type => type != typeof(DbContext) && typeof(DbContext).IsAssignableFrom(type));
    }
}
