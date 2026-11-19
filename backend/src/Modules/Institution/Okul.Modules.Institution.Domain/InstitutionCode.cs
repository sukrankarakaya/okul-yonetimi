using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Institution.Domain;

/// <summary>
/// Kurum kodu rules for the kurum itself (spec 0003, Requirement 1). The normalization lives in the module's contract
/// (<see cref="InstitutionCodeFormat"/>) so that other modules — Kimlik's login lock — use the very same one.
/// </summary>
public static class InstitutionCode
{
    public const int MinLength = InstitutionCodeFormat.MinLength;
    public const int MaxLength = InstitutionCodeFormat.MaxLength;

    /// <summary>The stored form of <paramref name="value"/>, or null when it can never be a kurum kodu.</summary>
    public static string? Normalize(string? value) => InstitutionCodeFormat.Normalize(value);

    /// <summary>Like <see cref="Normalize"/>, for creating a kurum: an invalid code is a programming error.</summary>
    public static string Require(string value) =>
        Normalize(value)
        ?? throw new ArgumentException(
            $"Kurum kodu {MinLength}–{MaxLength} karakter olmalı; yalnızca harf (A–Z), rakam ve tire.", nameof(value));
}
