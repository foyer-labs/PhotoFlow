// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Data;

/// <summary>
/// SQLite catalog (spec §9): one PC at a time (lock file held open exclusively), integrity check on open,
/// forward-only migrations with backup, WAL locally and rollback journal on network paths. A single connection
/// serialized by a semaphore: SQLite serializes writes anyway and this avoids busy errors.
/// </summary>
public sealed class SqliteCatalog : ICatalog
{
    private readonly SqliteConnection _connection;
    private readonly FileStream _lock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SqliteCatalog(SqliteConnection connection, FileStream lockStream)
    {
        _connection = connection;
        _lock = lockStream;
        Libraries = new LibraryStore(this);
        Assets = new AssetStore(this);
        Jobs = new JobStore(this);
        Decisions = new DecisionStore(this);
        XmpSync = new XmpSyncStore(this);
    }

    public ILibraryStore Libraries { get; }

    public IAssetStore Assets { get; }

    public IJobStore Jobs { get; }

    public IDecisionStore Decisions { get; }

    public IXmpSyncStore XmpSync { get; }

    public static async Task<SqliteCatalog> OpenAsync(CatalogOpenOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.CatalogPath))!);
        var lockStream = AcquireLock(options.CatalogPath);
        try
        {
            await CheckIntegrityAsync(options.CatalogPath, ct).ConfigureAwait(false);
            await CatalogMigrator.ApplyAsync(options, CatalogMigrator.Embedded(), ct).ConfigureAwait(false);
            var connection = Connections.Open(options.CatalogPath);
            await Connections.ExecuteAsync(connection, $"PRAGMA journal_mode = {CatalogJournal.ModeFor(options.CatalogPath)};", ct).ConfigureAwait(false);
            return new SqliteCatalog(connection, lockStream);
        }
        catch
        {
            await lockStream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken ct) =>
        await RunAsync(c => c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COALESCE(MAX(Version), 0) FROM SchemaVersion;", cancellationToken: ct)), ct).ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            await _lock.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    internal async Task<T> RunAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await action(_connection).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    internal Task RunAsync(Func<SqliteConnection, Task> action, CancellationToken ct) =>
        RunAsync(async c =>
        {
            await action(c).ConfigureAwait(false);
            return true;
        }, ct);

    private static FileStream AcquireLock(string catalogPath)
    {
        var lockPath = catalogPath + ".lock";
        try
        {
            // Held open without sharing for the whole session: released by the OS even after a crash, never deleted.
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            stream.SetLength(0);
            var owner = Encoding.UTF8.GetBytes($"{Environment.MachineName} pid {Environment.ProcessId} {DateTimeOffset.Now:O}");
            stream.Write(owner);
            stream.Flush(flushToDisk: true);
            return stream;
        }
        catch (IOException e)
        {
            var holder = TryReadOwner(lockPath);
            throw new CatalogLockedException($"The catalog is open on another PC or process{(holder is null ? "" : $" ({holder})")}: {e.Message}");
        }
    }

    private static string? TryReadOwner(string lockPath)
    {
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task CheckIntegrityAsync(string catalogPath, CancellationToken ct)
    {
        if (!File.Exists(catalogPath) || new FileInfo(catalogPath).Length == 0)
        {
            return;
        }

        try
        {
            await using var connection = Connections.Open(catalogPath, readOnly: true);
            var result = await connection.ExecuteScalarAsync<string>(new CommandDefinition("PRAGMA quick_check;", cancellationToken: ct)).ConfigureAwait(false);
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new CatalogCorruptException($"Catalog integrity check failed: {result}");
            }
        }
        catch (SqliteException e)
        {
            throw new CatalogCorruptException($"The catalog file is damaged or not a catalog: {e.Message}");
        }
    }
}

/// <summary>Value conversions shared by the stores (instants as Unix milliseconds, enums as names).</summary>
internal static class Db
{
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static long? Ms(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds();

    public static DateTimeOffset? Instant(long? ms, long? offsetMinutes = null) =>
        ms is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).ToOffset(TimeSpan.FromMinutes(offsetMinutes ?? 0));

    public static string? Name<TEnum>(TEnum? value)
        where TEnum : struct, Enum => value?.ToString();

    public static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(value);

    public static TEnum? ParseOrNull<TEnum>(string? value)
        where TEnum : struct, Enum => value is null ? null : Enum.Parse<TEnum>(value);

    public static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? Date(string? value) =>
        value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
