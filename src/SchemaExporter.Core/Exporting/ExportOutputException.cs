#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Exception thrown when output generation or file handling fails.
/// </summary>
public sealed class ExportOutputException : ExportWorkflowException {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportOutputException"/> class.
    /// </summary>
    /// <param name="message">The output-related error message.</param>
    public ExportOutputException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportOutputException"/> class.
    /// </summary>
    /// <param name="message">The output-related error message.</param>
    /// <param name="innerException">The underlying exception that caused the failure.</param>
    public ExportOutputException(string message, Exception innerException) : base(message, innerException) { }
}
