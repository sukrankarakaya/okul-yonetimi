using System.Reflection;

namespace Okul.Modules.Enrollment.Application;

/// <summary>Marker for handler registration (mediator) and architecture tests.</summary>
public static class EnrollmentApplicationAssembly
{
    public static Assembly Assembly => typeof(EnrollmentApplicationAssembly).Assembly;
}
