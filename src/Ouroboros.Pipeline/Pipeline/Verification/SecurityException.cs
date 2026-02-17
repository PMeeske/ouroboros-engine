namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Exception thrown when a security guard rail is violated.
/// </summary>
public class SecurityException : Exception
{
    /// <summary>
    /// Gets the action that violated the guard rail.
    /// </summary>
    public string? ViolatingAction { get; }

    /// <summary>
    /// Gets the context in which the violation occurred.
    /// </summary>
    public SafeContext? Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SecurityException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="violatingAction">The action that caused the violation.</param>
    /// <param name="context">The security context.</param>
    public SecurityException(string message, string violatingAction, SafeContext context)
        : base(message)
    {
        this.ViolatingAction = violatingAction;
        this.Context = context;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SecurityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}