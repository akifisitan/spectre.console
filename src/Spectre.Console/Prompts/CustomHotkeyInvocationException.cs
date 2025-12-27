namespace Spectre.Console;

/// <summary>
/// Indicates that the tree being rendered includes a cycle, and cannot be rendered.
/// </summary>
public sealed class CustomHotkeyInvocationException : Exception
{
    /// <summary>
    /// The registration key.
    /// </summary>
    public string Key { get; }

    internal CustomHotkeyInvocationException(string key)
    {
        Key = key;
    }
}
