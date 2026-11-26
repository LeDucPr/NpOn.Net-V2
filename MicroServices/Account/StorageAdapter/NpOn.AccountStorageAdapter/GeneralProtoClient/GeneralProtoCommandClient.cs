using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Microsoft.Extensions.DependencyInjection;
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
}

public sealed class GeneralProtoCommandClient(FldMasterProtoService.FldMasterProtoServiceClient protoClient)
    : IGeneralCommandClient
{
    public async Task<NpOnDbExecuteCommand?> GetExecCommandAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        GetExecCommandReply reply = await protoClient.GetExecCommandAsync(
            new GetExecCommandRequest { Code = code },
            cancellationToken: cancellationToken);

        if (!reply.Status || reply.Data == null)
            return null;

        CommandSpec spec = reply.Data;
        var databaseType = (EDb)spec.DatabaseType;
        var execType = (EExecType)spec.ExecType;

        var parameters = new INpOnDbCommandParam[spec.Parameters.Count];
        for (int i = 0; i < spec.Parameters.Count; i++)
        {
            CommandParamSpec p = spec.Parameters[i];
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
            services.AddTransient<IGeneralCommandClient, GeneralProtoCommandClient>();
            return Task.CompletedTask;
        };
}
