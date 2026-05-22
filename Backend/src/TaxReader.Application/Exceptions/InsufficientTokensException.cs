namespace TaxReader.Application.Exceptions;

public sealed class InsufficientTokensException : Exception
{
    public InsufficientTokensException() : base("Insufficient token balance to process this request.") { }
    public InsufficientTokensException(string message) : base(message) { }
    public InsufficientTokensException(string message, Exception inner) : base(message, inner) { }
}
