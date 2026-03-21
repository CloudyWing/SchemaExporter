#nullable enable

namespace CloudyWing.SchemaExporter.Exporting;

/// <summary>
/// Base exception for export workflow failures.
/// </summary>
public abstract class ExportWorkflowException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportWorkflowException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    protected ExportWorkflowException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}
