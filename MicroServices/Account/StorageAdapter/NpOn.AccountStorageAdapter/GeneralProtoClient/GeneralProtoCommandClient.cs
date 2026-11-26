using System.Collections.Concurrent;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NpOn.CommonGrpcCall;
using NpOn.Protos.General.V1;
using NpgsqlTypes;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;

/// <summary>
/// Local Schema-First gRPC adapter interface for AccountStorageAdapter.
/// Replaces the cross-service NpOn.IGeneralService DLL contract dependency.
/// </summary>
public interface IGeneralCommandClient
{
    Task<NpOnDbExecuteCommand?> GetExecCommandAsync(string code, CancellationToken cancellationToken = default);
    Task<int> RefreshCodesAsync(string[]? codes, bool isFullReload = false, CancellationToken cancellationToken = default);
}

public interface IGeneralCommandCache
{
    bool TryGet(string code, out CommandRModel? spec);
    void AddOrUpdate(string code, CommandRModel spec);
    string[] GetCachedCodes();
}

public sealed class GeneralCommandL1Cache : IGeneralCommandCache
{
    private readonly ConcurrentDictionary<string, CommandRModel> _cache = new(StringComparer.Ordinal);

    public bool TryGet(string code, out CommandRModel? spec) => _cache.TryGetValue(code, out spec);

    public void AddOrUpdate(string code, CommandRModel spec) => _cache[code] = spec;

    public string[] GetCachedCodes() => _cache.Keys.ToArray();
}

public sealed class GeneralProtoCommandClient(
    FldMasterProtoService.FldMasterProtoServiceClient protoClient,
    IGeneralCommandCache? l1Cache = null
) : IGeneralCommandClient
{
    private readonly IGeneralCommandCache _cache = l1Cache ?? new GeneralCommandL1Cache();

    public async Task<NpOnDbExecuteCommand?> GetExecCommandAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        if (_cache.TryGet(code, out var cachedSpec) && cachedSpec != null)
            return CloneExecuteCommand(cachedSpec);

        CommandRModelResponseProto reply = await protoClient.GetExecCommandAsync(
            new TblFldExecutionCommand { Code = code },
            cancellationToken: cancellationToken);

        if (!reply.Status || reply.Data == null)
            return null;

        CommandRModel spec = reply.Data;
        _cache.AddOrUpdate(code, spec);
        return CloneExecuteCommand(spec);
    }

    public async Task<int> RefreshCodesAsync(
        string[]? codes,
        bool isFullReload = false,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<string> targetCodes = isFullReload
            ? _cache.GetCachedCodes().Concat(codes ?? []).Distinct(StringComparer.Ordinal)
            : (codes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal);

        int refreshed = 0;
        foreach (string code in targetCodes)
        {
            if (!isFullReload && !_cache.TryGet(code, out _))
                continue;

            try
            {
                CommandRModelResponseProto reply = await protoClient.GetExecCommandAsync(
                    new TblFldExecutionCommand { Code = code },
                    cancellationToken: cancellationToken);

                if (reply.Status && reply.Data != null)
                {
                    _cache.AddOrUpdate(code, reply.Data);
                    refreshed++;
                }
            }
            catch
            {
                // Preserve previous valid cached spec on fetch failure for zero-downtime reads.
            }
        }

        return refreshed;
    }

    public static NpOnDbExecuteCommand CloneExecuteCommand(CommandRModel spec)
    {
        var databaseType = (EDb)spec.DatabaseType;
        var execType = (EExecType)spec.ExecType;

        var parameters = new INpOnDbCommandParam[spec.Parameters.Count];
        for (int i = 0; i < spec.Parameters.Count; i++)
        {
            NpOnDbCommandParamRModel p = spec.Parameters[i];
            object? value = p.HasParamValue ? p.ParamValue : null;
            parameters[i] = CreateDbParam(databaseType, p.ParamName, value, p.HasParamType ? p.ParamType : null);
        }

        return new NpOnDbExecuteCommand
        {
            CommandText = spec.CommandText,
            ExecType = execType,
            Parameters = parameters
        };
    }

    private static INpOnDbCommandParam CreateDbParam(EDb databaseType, string name, object? value, int? paramType)
    {
        if (paramType.HasValue && (databaseType == EDb.Postgres || databaseType == EDb.YugaBytePg))
        {
            return new NpOnDbCommandParam<NpgsqlDbType>(name, value, (NpgsqlDbType)paramType.Value);
        }

        return new NpOnDbCommandParam(name, value, paramType);
    }
}

public sealed class GeneralProtoClientResolver : SharedGrpcClientResolver
{
    protected override Func<IServiceCollection, string, Task> RegistrationAction =>
        (services, url) =>
        {
            services.RegisterSchemaFirstGrpcClientLoadBalancing<FldMasterProtoService.FldMasterProtoServiceClient>(url);
            services.TryAddSingleton<IGeneralCommandCache, GeneralCommandL1Cache>();
            services.AddTransient<IGeneralCommandClient, GeneralProtoCommandClient>();
            return Task.CompletedTask;
        };
}
