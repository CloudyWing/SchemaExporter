#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Exception thrown when export validation fails.
/// </summary>
public sealed class ExportValidationException : ExportWorkflowException {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportValidationException"/> class.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    public ExportValidationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportValidationException"/> class.
    /// </summary>
    /// <param name="message">The validation error message.</param>
    /// <param name="innerException">The inner exception that caused this validation failure.</param>
    public ExportValidationException(string message, Exception innerException) : base(message, innerException) { }
}
