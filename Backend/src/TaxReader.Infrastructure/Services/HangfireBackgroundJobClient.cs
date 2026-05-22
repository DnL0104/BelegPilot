using System.Linq.Expressions;
using Hangfire;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Infrastructure adapter — bridges the Application IBackgroundJobClient port to
/// Hangfire's framework interface. Naming collision: alias Hangfire's interface
/// via the using directive; declare the Application's interface fully-qualified.
/// </summary>
public class HangfireBackgroundJobClient(Hangfire.IBackgroundJobClient hangfireClient)
    : Application.Interfaces.IBackgroundJobClient
{
    public Task<string> EnqueueAsync<TJob>(
        Expression<Func<TJob, Task>> methodCall,
        CancellationToken cancellationToken = default)
    {
        var jobId = hangfireClient.Enqueue(methodCall);
        return Task.FromResult(jobId);
    }

    public Task DeleteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        hangfireClient.Delete(jobId);
        return Task.CompletedTask;
    }
}
