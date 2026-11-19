using System.Reflection;

namespace Okul.Modules.Institution.Application;

/// <summary>Marker for handler registration (mediator) and architecture tests.</summary>
public static class InstitutionApplicationAssembly
{
    public static Assembly Assembly => typeof(InstitutionApplicationAssembly).Assembly;
}
