// SPDX-License-Identifier: Apache-2.0

using Dapper;
using Microsoft.Data.Sqlite;
using PhotoFlow.Core.Catalog;

namespace PhotoFlow.Data;

internal sealed class GroupStore(SqliteCatalog catalog) : IGroupStore
{
    public async Task<IReadOnlyList<(AssetGroup Group, IReadOnlyList<GroupMember> Members)>> ReplaceAutomaticAsync(
        long jobId, IReadOnlyList<(GroupType Type, IReadOnlyList<long> AssetIds)> groups, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(groups);
        await catalog.RunAsync(async c =>
        {
            using var transaction = c.BeginTransaction();
            var automatic = new { jobId, origin = nameof(GroupOrigin.Automatic) };
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM GroupMembers WHERE GroupId IN (SELECT Id FROM Groups WHERE JobId = @jobId AND Origin = @origin);",
                automatic, transaction, cancellationToken: ct)).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Groups WHERE JobId = @jobId AND Origin = @origin;", automatic, transaction, cancellationToken: ct)).ConfigureAwait(false);
            var inManual = (await c.QueryAsync<long>(new CommandDefinition(
                "SELECT m.AssetId FROM GroupMembers m JOIN Groups g ON g.Id = m.GroupId WHERE g.JobId = @jobId;",
                new { jobId }, transaction, cancellationToken: ct)).ConfigureAwait(false)).ToHashSet();

            foreach (var (type, assetIds) in groups)
            {
                var members = assetIds.Where(a => !inManual.Contains(a)).Distinct().ToList();
                if (members.Count < 2)
                {
                    continue;
                }

                await InsertAsync(c, transaction, jobId, type, GroupOrigin.Automatic, members, ct).ConfigureAwait(false);
            }

            transaction.Commit();
        }, ct).ConfigureAwait(false);
        return await ListAsync(jobId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(AssetGroup Group, IReadOnlyList<GroupMember> Members)>> ListAsync(long jobId, CancellationToken ct)
    {
        var (groups, members) = await catalog.RunAsync(async c =>
        {
            var g = (await c.QueryAsync<GroupRow>(new CommandDefinition(
                "SELECT Id, JobId, Type, ProfileId, ProfileConfidence, Origin FROM Groups WHERE JobId = @jobId ORDER BY Id;",
                new { jobId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
            var m = (await c.QueryAsync<MemberRow>(new CommandDefinition(
                "SELECT m.GroupId, m.AssetId, m.Position, m.Score FROM GroupMembers m JOIN Groups g ON g.Id = m.GroupId WHERE g.JobId = @jobId ORDER BY m.GroupId, m.Position;",
                new { jobId }, cancellationToken: ct)).ConfigureAwait(false)).ToList();
            return (g, m);
        }, ct).ConfigureAwait(false);
        var byGroup = members.GroupBy(m => m.GroupId).ToDictionary(g => g.Key, g => (IReadOnlyList<GroupMember>)g.Select(m => new GroupMember(m.GroupId, m.AssetId, (int)m.Position, m.Score)).ToList());
        return groups.Select(g => (
                new AssetGroup(g.Id, g.JobId, Db.Parse<GroupType>(g.Type), g.ProfileId, g.ProfileConfidence, Db.Parse<GroupOrigin>(g.Origin)),
                byGroup.GetValueOrDefault(g.Id) ?? []))
            .ToList();
    }

    public Task<long> AddManualAsync(long jobId, GroupType type, IReadOnlyList<long> assetIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        return catalog.RunAsync(async c =>
        {
            using var transaction = c.BeginTransaction();

            // A photo belongs to one group of the job: the manual group takes it from any automatic one.
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM GroupMembers WHERE AssetId IN @assetIds AND GroupId IN (SELECT Id FROM Groups WHERE JobId = @jobId);",
                new { assetIds, jobId }, transaction, cancellationToken: ct)).ConfigureAwait(false);
            var id = await InsertAsync(c, transaction, jobId, type, GroupOrigin.Manual, assetIds.Distinct().ToList(), ct).ConfigureAwait(false);
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM Groups WHERE JobId = @jobId AND Id NOT IN (SELECT DISTINCT GroupId FROM GroupMembers);",
                new { jobId }, transaction, cancellationToken: ct)).ConfigureAwait(false);
            transaction.Commit();
            return id;
        }, ct);
    }

    public Task SetScoresAsync(long groupId, IReadOnlyDictionary<long, double> scores, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scores);
        return catalog.RunAsync(async c =>
        {
            using var transaction = c.BeginTransaction();
            foreach (var (assetId, score) in scores)
            {
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE GroupMembers SET Score = @score WHERE GroupId = @groupId AND AssetId = @assetId;",
                    new { groupId, assetId, score }, transaction, cancellationToken: ct)).ConfigureAwait(false);
            }

            transaction.Commit();
        }, ct);
    }

    public Task SetProfileAsync(long groupId, string? profileId, double? confidence, CancellationToken ct) =>
        catalog.RunAsync(c => c.ExecuteAsync(new CommandDefinition(
            "UPDATE Groups SET ProfileId = @profileId, ProfileConfidence = @confidence WHERE Id = @groupId;",
            new { groupId, profileId, confidence }, cancellationToken: ct)), ct);

    private static async Task<long> InsertAsync(SqliteConnection c, SqliteTransaction transaction, long jobId, GroupType type, GroupOrigin origin, IReadOnlyList<long> members, CancellationToken ct)
    {
        var id = await c.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO Groups (JobId, Type, Origin, CreatedAtMs) VALUES (@jobId, @type, @origin, @now); SELECT last_insert_rowid();",
            new { jobId, type = type.ToString(), origin = origin.ToString(), now = Db.Now() }, transaction, cancellationToken: ct)).ConfigureAwait(false);
        for (var i = 0; i < members.Count; i++)
        {
            await c.ExecuteAsync(new CommandDefinition(
                "INSERT INTO GroupMembers (GroupId, AssetId, Position) VALUES (@id, @assetId, @position);",
                new { id, assetId = members[i], position = i }, transaction, cancellationToken: ct)).ConfigureAwait(false);
        }

        return id;
    }

    private sealed record GroupRow(long Id, long JobId, string Type, string? ProfileId, double? ProfileConfidence, string Origin);

    private sealed record MemberRow(long GroupId, long AssetId, long Position, double? Score);
}
