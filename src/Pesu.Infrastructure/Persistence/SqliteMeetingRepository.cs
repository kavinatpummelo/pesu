using System.Text.Json;
using Microsoft.Data.Sqlite;
using Pesu.Core.Models;
using Pesu.Core.Services;

namespace Pesu.Infrastructure.Persistence;

public sealed class SqliteMeetingRepository : IMeetingRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public SqliteMeetingRepository(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? ".");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        EnsureSchema();
    }

    public async Task<IReadOnlyList<Meeting>> ListAsync(CancellationToken cancellationToken = default)
    {
        var meetings = new List<Meeting>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, started_at, duration_seconds, summary, decisions_json, transcript_json,
                   system_audio_path, microphone_audio_path, is_all_day, calendar_name
            FROM meetings
            ORDER BY started_at DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            meetings.Add(new Meeting(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                TimeSpan.FromSeconds(reader.GetInt32(3)),
                reader.GetString(4),
                JsonSerializer.Deserialize<List<Decision>>(reader.GetString(5), SerializerOptions) ?? new List<Decision>(),
                JsonSerializer.Deserialize<List<TranscriptSegment>>(reader.GetString(6), SerializerOptions) ?? new List<TranscriptSegment>(),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9) == 1,
                reader.IsDBNull(10) ? null : reader.GetString(10)
            ));
        }

        return meetings;
    }

    public async Task<Meeting> SaveAsync(Meeting meeting, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (meeting.Id == 0)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO meetings (
                    title, started_at, duration_seconds, summary, decisions_json, transcript_json,
                    system_audio_path, microphone_audio_path, is_all_day, calendar_name
                ) VALUES (
                    $title, $startedAt, $durationSeconds, $summary, $decisions, $transcript,
                    $systemAudioPath, $microphoneAudioPath, $isAllDay, $calendarName
                );
                SELECT last_insert_rowid();
                """;
            BindMeeting(insert, meeting);
            var newId = (long)(await insert.ExecuteScalarAsync(cancellationToken) ?? 0L);
            return meeting with { Id = newId };
        }

        await using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE meetings
            SET title = $title,
                started_at = $startedAt,
                duration_seconds = $durationSeconds,
                summary = $summary,
                decisions_json = $decisions,
                transcript_json = $transcript,
                system_audio_path = $systemAudioPath,
                microphone_audio_path = $microphoneAudioPath,
                is_all_day = $isAllDay,
                calendar_name = $calendarName
            WHERE id = $id;
            """;
        BindMeeting(update, meeting);
        update.Parameters.AddWithValue("$id", meeting.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);

        return meeting;
    }

    public async Task DeleteAsync(long meetingId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM meetings WHERE id = $id;";
        command.Parameters.AddWithValue("$id", meetingId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void BindMeeting(SqliteCommand command, Meeting meeting)
    {
        command.Parameters.AddWithValue("$title", meeting.Title);
        command.Parameters.AddWithValue("$startedAt", meeting.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$durationSeconds", (int)Math.Max(1, meeting.Duration.TotalSeconds));
        command.Parameters.AddWithValue("$summary", meeting.Summary);
        command.Parameters.AddWithValue("$decisions", JsonSerializer.Serialize(meeting.Decisions, SerializerOptions));
        command.Parameters.AddWithValue("$transcript", JsonSerializer.Serialize(meeting.Transcript, SerializerOptions));
        command.Parameters.AddWithValue("$systemAudioPath", (object?)meeting.SystemAudioPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$microphoneAudioPath", (object?)meeting.MicrophoneAudioPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$isAllDay", meeting.IsAllDay ? 1 : 0);
        command.Parameters.AddWithValue("$calendarName", (object?)meeting.CalendarName ?? DBNull.Value);
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS meetings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                started_at TEXT NOT NULL,
                duration_seconds INTEGER NOT NULL,
                summary TEXT NOT NULL,
                decisions_json TEXT NOT NULL,
                transcript_json TEXT NOT NULL,
                system_audio_path TEXT NULL,
                microphone_audio_path TEXT NULL,
                is_all_day INTEGER NOT NULL DEFAULT 0,
                calendar_name TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
