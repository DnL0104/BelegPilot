namespace TaxReader.Application.Exceptions;

public sealed class ParserNotFoundException : Exception
{
    public ParserNotFoundException() : base("No suitable parser found for this document format.") { }
    public ParserNotFoundException(string message) : base(message) { }
    public ParserNotFoundException(string message, Exception inner) : base(message, inner) { }
}
