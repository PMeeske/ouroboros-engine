namespace Ouroboros.Providers;

/// <summary>
/// Void result type for operations with no meaningful return value.
/// Named VoidResult to avoid conflict with Unit.
/// </summary>
public readonly struct VoidResult
{
    public static readonly VoidResult Value = default;
}