namespace Spectre.Console;

/// <summary>
/// The exception thrown when a registered custom hotkey interrupts a prompt.
/// </summary>
/// <remarks>
/// Use <see cref="Key"/> to identify the hotkey registration that was invoked.
/// </remarks>
public sealed class CustomHotkeyInvocationException : Exception
{
    /// <summary>
    /// Gets the registration key of the custom hotkey that interrupted the prompt.
    /// </summary>
    public string Key { get; }

    internal CustomHotkeyInvocationException(string key)
    {
        Key = key;
    }
}
