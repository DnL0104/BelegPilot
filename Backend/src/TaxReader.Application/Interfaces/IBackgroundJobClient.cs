using System.Linq.Expressions;

namespace TaxReader.Application.Interfaces;

/// <summary>
/// Application port wrapping Hangfire's IBackgroundJobClient so Application stays
/// Hangfire-storage-free (per architecture rule "Application defines interfaces only").
/// The expression-of-typed-method-call signature is Hangfire's native idiom and is
/// the only Application-friendly way to encode "enqueue this typed call" without
/// referencing Hangfire.AspNetCore or Hangfire.PostgreSql.
/// </summary>
public interface IBackgroundJobClient
{
    Task<string> EnqueueAsync<TJob>(
        Expression<Func<TJob, Task>> methodCall,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string jobId, CancellationToken cancellationToken = default);
}
