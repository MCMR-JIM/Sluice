namespace Sluice;

/// <summary>Base type for expected, user-facing failures.</summary>
public class SluiceException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Human readable description.</param>
    public SluiceException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with a message and inner exception.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying failure.</param>
    public SluiceException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>Raised when a Store product cannot be turned into downloadable packages.</summary>
public sealed class StoreResolutionException : SluiceException
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">Human readable description.</param>
    /// <param name="innerException">Underlying failure.</param>
    public StoreResolutionException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}
