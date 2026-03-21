#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Exception thrown when database schema loading fails.
/// </summary>
public sealed class ExportConnectionException : ExportWorkflowException {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportConnectionException"/> class.
    /// </summary>
    /// <param name="message">The connection-related error message.</param>
    /// <param name="innerException">The underlying exception that caused the failure.</param>
    public ExportConnectionException(string message, Exception innerException) : base(message, innerException) { }
}
