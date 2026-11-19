using System.Reflection;

namespace Okul.Modules.Identity.Application;

/// <summary>Marker for handler registration (mediator) and architecture tests.</summary>
public static class IdentityApplicationAssembly
{
    public static Assembly Assembly => typeof(IdentityApplicationAssembly).Assembly;
}
