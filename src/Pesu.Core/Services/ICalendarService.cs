namespace Pesu.Core.Services;

public interface ICalendarService
{
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
