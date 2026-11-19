namespace Okul.Testing.Database;

/// <summary>
/// What the test server must offer before any test runs (spec 0002, CB-4): PostgreSQL major version 18
/// (patch versions may differ — plan 0002, R6) and the ICU collation the Turkish ordering proof needs (0001/AC-7).
/// </summary>
public static class ServerRequirements
{
    public const int RequiredMajorVersion = 18;
    public const string Collation = "tr-TR-x-icu";

    /// <summary>The reason the server is unfit, or null when it meets the requirements.</summary>
    /// <param name="serverVersionNum">PostgreSQL's <c>server_version_num</c>, e.g. 180001.</param>
    public static string? Problem(int serverVersionNum, bool hasCollation)
    {
        var major = serverVersionNum / 10000;
        if (major != RequiredMajorVersion)
        {
            return $"Test sunucusu PostgreSQL {RequiredMajorVersion} olmalı; bulunan ana sürüm: {major}.";
        }

        return hasCollation
            ? null
            : $"Test sunucusunda '{Collation}' collation'ı yok: ICU destekli bir PostgreSQL {RequiredMajorVersion} kurulumu gerekir.";
    }
}
