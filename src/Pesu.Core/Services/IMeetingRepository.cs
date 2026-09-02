using Pesu.Core.Models;

namespace Pesu.Core.Services;

public interface IMeetingRepository
{
    Task<IReadOnlyList<Meeting>> ListAsync(CancellationToken cancellationToken = default);
    Task<Meeting> SaveAsync(Meeting meeting, CancellationToken cancellationToken = default);
    Task DeleteAsync(long meetingId, CancellationToken cancellationToken = default);
}
