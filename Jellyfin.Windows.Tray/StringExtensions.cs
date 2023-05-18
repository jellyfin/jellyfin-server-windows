#nullable enable
namespace Jellyfin.Windows.Tray;

/// <summary>
///     Helper Methods for Strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    ///     Truncates the input string to a max size and appends the suffix if truncated.
    /// </summary>
    /// <param name="value">The input string to truncate.</param>
    /// <param name="maxLength">The Maximum Length.</param>
    /// <param name="truncationSuffix">The suffix to append when Truncated.</param>
    /// <returns>The original string if not exceeding <paramref name="maxLength"/> otherwise the truncated string with <paramref name="truncationSuffix"/>.</returns>
    public static string? Truncate(this string? value, int maxLength, string truncationSuffix = "…")
    {
        return value?.Length > maxLength
            ? value.Substring(0, maxLength) + truncationSuffix
            : value;
    }
}
