namespace MediaVault.Logging;

public sealed class UnityLoggerOptions {
    /// <summary>
    /// Determines whether to enable colored output in logging.
    /// </summary>
    public bool EnableColoredOutput { get; set; } = true;

    /// <summary>
    /// Whether to trim namespaces from category names
    /// </summary>
    public bool TrimNamespaces { get; set; } = false;

    /// <summary>
    /// Number of namespace segments to keep when trimming (0 = class name only, 1 = last namespace and class, etc.)
    /// Only used when TrimNamespaces is true
    /// </summary>
    public int NamespaceSegmentsToKeep { get; set; } = 0;

    /// <summary>
    /// Processes the category name based on namespace trimming options and the number of namespace segments to keep.
    /// </summary>
    /// <param name="categoryName">The fully qualified name of the category to be processed.</param>
    /// <returns>A processed category name based on the trimming configuration, or the original category name if trimming is disabled.</returns>
    public string ProcessCategoryName(string categoryName) {
        if (!TrimNamespaces || NamespaceSegmentsToKeep < 0)
            return categoryName;

        // Apply segment limiting
        var segments = categoryName.Split('.');
        if (segments.Length > NamespaceSegmentsToKeep + 1) {
            var segmentsToTake = Math.Max(1, NamespaceSegmentsToKeep + 1);
            var startIndex = segments.Length - segmentsToTake;
            categoryName = string.Join(".", segments.Skip(startIndex));
        }

        return categoryName;
    }
}
