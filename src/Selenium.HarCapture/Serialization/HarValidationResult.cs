using System.Collections.Generic;
using System.Linq;

namespace Selenium.HarCapture.Serialization;

/// <summary>
/// The result of a HAR validation pass. Contains all findings (errors and warnings).
/// </summary>
public sealed class HarValidationResult
{
    private readonly IReadOnlyList<HarValidationError> _errors;

    /// <summary>All validation findings (errors and warnings) found during validation.</summary>
    public IReadOnlyList<HarValidationError> Errors => _errors;

    /// <summary>
    /// True when no Error-severity findings exist. Warnings alone do not invalidate the result.
    /// </summary>
    public bool IsValid => !_errors.Any(e => e.Severity == HarValidationSeverity.Error);

    /// <summary>All Warning-severity findings.</summary>
    public IEnumerable<HarValidationError> Warnings =>
        _errors.Where(e => e.Severity == HarValidationSeverity.Warning);

    internal HarValidationResult(List<HarValidationError> errors) =>
        _errors = errors.AsReadOnly();
}
