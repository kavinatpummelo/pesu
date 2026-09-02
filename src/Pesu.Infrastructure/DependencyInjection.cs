using Microsoft.Extensions.DependencyInjection;
using Pesu.Core.Services;
using Pesu.Core.ViewModels;
using Pesu.Infrastructure.Persistence;
using Pesu.Infrastructure.SampleData;
using Pesu.Infrastructure.Services;

namespace Pesu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPesuInfrastructure(this IServiceCollection services, string appDataRoot)
    {
        var dbPath = Path.Combine(appDataRoot, "Pesu", "pesu.sqlite3");
        services.AddSingleton<IMeetingRepository>(_ => new SqliteMeetingRepository(dbPath));
        services.AddSingleton<IAudioCaptureService, AudioCaptureServiceStub>();
        services.AddSingleton<ICalendarService, CalendarServiceStub>();
        services.AddSingleton<INotesService, LocalNotesServiceStub>();
        services.AddSingleton<ICredentialStore, InMemoryCredentialStore>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ISampleDataInitializer, SampleDataInitializer>();
        return services;
    }
}

public interface ISampleDataInitializer
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}

internal sealed class SampleDataInitializer : ISampleDataInitializer
{
    private readonly IMeetingRepository _meetingRepository;

    public SampleDataInitializer(IMeetingRepository meetingRepository)
    {
        _meetingRepository = meetingRepository;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _meetingRepository.ListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        foreach (var meeting in SampleMeetings.Create())
        {
            await _meetingRepository.SaveAsync(meeting, cancellationToken);
        }
    }
}
