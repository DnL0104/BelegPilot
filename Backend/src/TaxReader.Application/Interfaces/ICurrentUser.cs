namespace TaxReader.Application.Interfaces;

public interface ICurrentUser
{
    Guid UserId { get; }
}
