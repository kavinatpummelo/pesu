namespace Pesu.Core.Services;

public interface ICredentialStore
{
    Task SaveAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
