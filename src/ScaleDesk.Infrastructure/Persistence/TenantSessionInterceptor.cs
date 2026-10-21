using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ScaleDesk.Application.Abstractions;

namespace ScaleDesk.Infrastructure.Persistence;

/// <summary>
/// Binds the active tenant to the Postgres session so row-level security can enforce isolation as a
/// SECOND line of defence beneath the EF global query filter (spec §40). On every logical connection
/// open it issues <c>set_config</c>:
///   * tenant bound          -> app.tenant_id = &lt;id>, bypass off  => RLS filters to that tenant;
///   * no tenant (workers /  -> app.bypass_rls = 'on'               => RLS lets the cross-tenant
///     login / migrate /                                         scan through, mirroring the
///     seed)                                                         existing null-tenant filter.
/// Both GUCs are written on EVERY open — including with empty values — so a physical connection
/// recycled from the pool is always re-stamped before any query runs; there is no window in which a
/// leased connection carries a stale tenant, so no reset-on-close is required. Values are always
/// parameterised, never concatenated into SQL.
/// </summary>
public sealed class TenantSessionInterceptor(ICurrentTenant tenant) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
        => await ApplyAsync(connection, cancellationToken);

    private void Apply(DbConnection connection)
    {
        using var command = Build(connection);
        command.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        await using var command = Build(connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private DbCommand Build(DbConnection connection)
    {
        var command = connection.CreateCommand();
        var tenantId = tenant.TenantId;
        command.CommandText =
            "SELECT set_config('app.tenant_id', @tenant_id, false), set_config('app.bypass_rls', @bypass, false);";
        Add(command, "tenant_id", tenantId?.ToString() ?? string.Empty);
        Add(command, "bypass", tenantId is null ? "on" : string.Empty);
        return command;
    }

    private static void Add(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
