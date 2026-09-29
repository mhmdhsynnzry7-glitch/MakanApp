using System.Reflection;
using System.Xml.Linq;
using MakanApp.Application.Common;
using MakanApp.Domain.Common;
using Xunit;

namespace MakanApp.ArchitectureTests;

public sealed class OnionDependencyTests
{
    [Fact]
    public void DomainMustNotReferenceApplication() =>
        AssertProjectDoesNotReference("MakanApp.Domain", "MakanApp.Application");

    [Fact]
    public void DomainMustNotReferenceInfrastructure() =>
        AssertProjectDoesNotReference("MakanApp.Domain", "MakanApp.Infrastructure");

    [Fact]
    public void DomainMustNotReferenceApi() =>
        AssertProjectDoesNotReference("MakanApp.Domain", "MakanApp.Api");

    [Fact]
    public void ApplicationMustReferenceDomain() =>
        AssertProjectReferences("MakanApp.Application", "MakanApp.Domain");

    [Fact]
    public void ApplicationMustNotReferenceInfrastructure() =>
        AssertProjectDoesNotReference("MakanApp.Application", "MakanApp.Infrastructure");

    [Fact]
    public void ApplicationMustNotReferenceApi() =>
        AssertProjectDoesNotReference("MakanApp.Application", "MakanApp.Api");

    [Fact]
    public void InfrastructureMustReferenceApplicationAndDomain()
    {
        AssertProjectReferences("MakanApp.Infrastructure", "MakanApp.Application");
        AssertProjectReferences("MakanApp.Infrastructure", "MakanApp.Domain");
    }

    [Fact]
    public void InfrastructureMustNotReferenceApi() =>
        AssertProjectDoesNotReference("MakanApp.Infrastructure", "MakanApp.Api");

    [Fact]
    public void ApiMustReferenceApplicationAndInfrastructure()
    {
        AssertProjectReferences("MakanApp.Api", "MakanApp.Application");
        AssertProjectReferences("MakanApp.Api", "MakanApp.Infrastructure");
    }

    [Fact]
    public void DomainMustNotReferenceEntityFrameworkCore()
    {
        AssertAssemblyDoesNotReference(typeof(DomainAssembly).Assembly, "Microsoft.EntityFrameworkCore");
        AssertPackageDoesNotStartWith("MakanApp.Domain", "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void ApplicationMustNotReferenceEntityFrameworkCore()
    {
        AssertAssemblyDoesNotReference(typeof(ApplicationAssembly).Assembly, "Microsoft.EntityFrameworkCore");
        AssertPackageDoesNotStartWith("MakanApp.Application", "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void DomainMustNotReferenceAspNetCore()
    {
        AssertAssemblyDoesNotReference(typeof(DomainAssembly).Assembly, "Microsoft.AspNetCore");
        AssertPackageDoesNotStartWith("MakanApp.Domain", "Microsoft.AspNetCore");
    }

    private static void AssertProjectReferences(string projectName, string referencedProjectName)
    {
        var references = GetProjectReferences(projectName);
        Assert.Contains(referencedProjectName, references, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertProjectDoesNotReference(string projectName, string referencedProjectName)
    {
        var references = GetProjectReferences(projectName);
        Assert.DoesNotContain(referencedProjectName, references, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> GetProjectReferences(string projectName)
    {
        var document = XDocument.Load(GetProjectPath(projectName));
        return document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFileNameWithoutExtension(value!))
            .ToArray();
    }

    private static void AssertPackageDoesNotStartWith(string projectName, string forbiddenPrefix)
    {
        var document = XDocument.Load(GetProjectPath(projectName));
        var packages = document.Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value));

        Assert.DoesNotContain(
            packages,
            package => package!.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertAssemblyDoesNotReference(Assembly assembly, string forbiddenPrefix)
    {
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string GetProjectPath(string projectName) =>
        Path.Combine(GetSolutionRoot(), projectName, $"{projectName}.csproj");

    private static string GetSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MakanApp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("MakanApp.sln could not be located.");
    }
}
