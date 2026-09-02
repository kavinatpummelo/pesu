using System.Collections.Concurrent;
using Pesu.Core.Services;

namespace Pesu.Infrastructure.Services;

public sealed class InMemoryCredentialStore : ICredentialStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
