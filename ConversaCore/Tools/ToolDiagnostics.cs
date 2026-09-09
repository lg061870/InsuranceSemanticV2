namespace ConversaCore.Tools;

/// <summary>Classifies a tool invocation diagnostic.</summary>
public enum ToolDiagnosticKind { Invoking, Completed, Latency, PolicyRejected, Failed }

/// <summary>Immutable, payload-free tool invocation diagnostic.</summary>
public sealed record ToolDiagnostic(
    ToolDiagnosticKind Kind, string ToolId, string Version, string CorrelationId,
    TimeSpan Duration, string? ErrorCode = null, bool Sensitive = false);

/// <summary>Receives structured tool diagnostics; implementations must not block execution.</summary>
public interface IToolDiagnostics
{
    /// <summary>Records one payload-free diagnostic.</summary>
    void Record(ToolDiagnostic diagnostic);
}
