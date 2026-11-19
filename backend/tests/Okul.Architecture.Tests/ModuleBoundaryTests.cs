using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetArchTest.Rules;
using Shouldly;

namespace Okul.Architecture.Tests;

/// <summary>
/// docs/architecture.md, Forbidden dependencies. The module list comes from <c>Okul.slnx</c>
/// (every <c>src/Modules/**/Okul.Modules.&lt;Module&gt;.&lt;Layer&gt;.csproj</c>), so a new module is covered
/// without editing this file — and a module that is not loaded fails <see cref="EverySolutionModuleIsLoaded"/>
/// instead of silently escaping the rules (closed, not open).
/// Rules 4 (Tahsilat ↛ AI) and 5 (AI ↛ sending) get their tests with those modules.
/// </summary>
public sealed partial class ModuleBoundaryTests
{
    private static readonly IReadOnlyList<string> SolutionModuleAssemblies = ReadSolutionModuleAssemblies();
    private static readonly IReadOnlyList<Assembly> ModuleAssemblies = LoadModuleAssemblies();

    public static TheoryData<string> AllModuleAssemblies() => new(SolutionModuleAssemblies);

    public static TheoryData<string> DomainAssemblies() => Layer("Domain");

    public static TheoryData<string> ApplicationAssemblies() => Layer("Application");

    [Fact]
    public void EverySolutionModuleIsLoaded()
    {
        SolutionModuleAssemblies.ShouldContain("Okul.Modules.Institution.Domain");
        ModuleAssemblies.Select(a => a.GetName().Name!).ShouldBe(SolutionModuleAssemblies, ignoreOrder: true,
            "every Okul.Modules.* project in Okul.slnx must be loadable here (reference it from Okul.WebApi)");
    }

    /// <summary>
    /// Rule 1 + 3: a module never references another module's Domain or Infrastructure (incl. its DbContext).
    /// Checked on the assembly's references by pattern, so it bites even while only one module exists.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllModuleAssemblies))]
    public void Module_DoesNotReference_OtherModulesDomainOrInfrastructure(string assemblyName)
    {
        var module = ModuleOf(assemblyName);
        var offending = Load(assemblyName).GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(name => ForeignInnerLayer().Match(name) is { Success: true } m && m.Groups["module"].Value != module)
            .ToList();

        offending.ShouldBeEmpty($"{assemblyName} references another module's Domain/Infrastructure");
    }

    /// <summary>Rule 2: Domain is framework-free.</summary>
    [Theory]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_DoesNotDependOn_Frameworks(string assemblyName)
    {
        AssertNoDependency(
            assemblyName,
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "System.Net.Http", "Npgsql", "Okul.Core",
            $"Okul.Modules.{ModuleOf(assemblyName)}.Application", $"Okul.Modules.{ModuleOf(assemblyName)}.Infrastructure");
    }

    /// <summary>Rule 6: external clients live only in Infrastructure; Application never sees Infrastructure.</summary>
    [Theory]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_DoesNotDependOn_InfrastructureOrExternalClients(string assemblyName)
    {
        AssertNoDependency(
            assemblyName,
            $"Okul.Modules.{ModuleOf(assemblyName)}.Infrastructure", "Npgsql", "System.Net.Http", "Microsoft.AspNetCore");
    }

    private static void AssertNoDependency(string assemblyName, params string[] forbidden)
    {
        forbidden.ShouldNotBeEmpty();
        var result = Types.InAssembly(Load(assemblyName)).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"{assemblyName} depends on a forbidden namespace ({string.Join(", ", forbidden)}): "
            + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [GeneratedRegex(@"^Okul\.Modules\.(?<module>[^.]+)\.(Domain|Infrastructure)$")]
    private static partial Regex ForeignInnerLayer();

    private static Assembly Load(string assemblyName) => ModuleAssemblies.Single(a => a.GetName().Name == assemblyName);

    private static TheoryData<string> Layer(string layer) =>
        new(SolutionModuleAssemblies.Where(n => n.EndsWith("." + layer, StringComparison.Ordinal)));

    private static string ModuleOf(string assemblyName) => assemblyName.Split('.')[2];

    private static IReadOnlyList<string> ReadSolutionModuleAssemblies()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Okul.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Okul.slnx not found above the test output directory");
        return XDocument.Load(Path.Combine(dir.FullName, "Okul.slnx"))
            .Descendants("Project")
            .Select(p => p.Attribute("Path")!.Value.Replace('\\', '/'))
            .Where(path => path.StartsWith("src/Modules/", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!)
            .Where(name => name.StartsWith("Okul.Modules.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<Assembly> LoadModuleAssemblies() =>
        Directory.GetFiles(AppContext.BaseDirectory, "Okul.Modules.*.dll")
            .Select(path => Assembly.LoadFrom(path))
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToList();
}
