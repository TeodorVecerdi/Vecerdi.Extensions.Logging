namespace Vecerdi.Extensions.Logging;

/// <summary>
/// Which log levels ask Unity to capture a stack trace. Capturing is the expensive part of a
/// <c>Debug.Log</c> call, so turning it off for chatty levels makes logging cheap without touching
/// the project-wide <c>Application.SetStackTraceLogType</c> setting.
/// </summary>
public enum StackTraceMode {
    /// <summary>Every entry gets a stack trace (Unity's own default).</summary>
    Always,

    /// <summary>Warnings, errors and critical entries get a stack trace; trace/debug/information do not.</summary>
    WarningsAndErrors,

    /// <summary>Only errors and critical entries get a stack trace.</summary>
    ErrorsOnly,

    /// <summary>No entry gets a stack trace. Exceptions still carry their own trace in the message text.</summary>
    Never,
}

/// <summary>Output options for the Unity console provider. Bound from <c>Logging:Unity</c> when configuration is used.</summary>
public sealed class UnityLoggerOptions {
    /// <summary>Colour the <c>[Level, Category]</c> header with rich text. Editor only; players always get plain text.</summary>
    public bool EnableColoredOutput { get; set; } = true;

    /// <summary>
    /// How many trailing segments of the category name to show in the header: <c>1</c> keeps just the
    /// type name (<c>Mixer</c>), <c>2</c> keeps <c>Audio.Mixer</c>, and so on. <c>null</c> shows the
    /// full category. Values below 1 are treated as 1.
    /// </summary>
    public int? CategorySegments { get; set; } = 1;

    /// <summary>Render the active logging scopes after the header, as <c>[key=value ...]</c>.</summary>
    public bool IncludeScopes { get; set; }

    /// <summary>Which levels ask Unity to capture a stack trace. See <see cref="StackTraceMode"/>.</summary>
    public StackTraceMode StackTraces { get; set; } = StackTraceMode.Always;

    /// <summary>Applies <see cref="CategorySegments"/> to a category name.</summary>
    internal string FormatCategory(string categoryName) {
        if (CategorySegments is not { } segments) {
            return categoryName;
        }

        segments = Math.Max(1, segments);
        var parts = categoryName.Split('.');
        if (parts.Length <= segments) {
            return categoryName;
        }

        var trimmed = string.Join(".", parts, parts.Length - segments, segments);
        return trimmed.Length == 0 ? categoryName : trimmed;
    }
}
