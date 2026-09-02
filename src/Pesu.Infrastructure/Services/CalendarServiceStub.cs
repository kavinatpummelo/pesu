using Pesu.Core.Services;

namespace Pesu.Infrastructure.Services;

public sealed class CalendarServiceStub : ICalendarService
{
    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
