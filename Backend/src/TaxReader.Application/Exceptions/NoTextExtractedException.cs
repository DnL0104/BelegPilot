namespace TaxReader.Application.Exceptions;

public sealed class NoTextExtractedException : Exception
{
    public NoTextExtractedException() : base("No text could be extracted from the document.") { }
    public NoTextExtractedException(string message) : base(message) { }
    public NoTextExtractedException(string message, Exception inner) : base(message, inner) { }
}
