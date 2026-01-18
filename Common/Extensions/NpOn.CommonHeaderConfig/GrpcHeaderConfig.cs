using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using Grpc.Core;

namespace Common.Extensions.NpOn.HeaderConfig;

public class GrpcHeaderConfig : IHeaderConfig<Metadata>
{
    // internal
    private const string GrpcInternalCallerUserNameInternal = "caller-user"; // client
    private const string GrpcInternalCallerMachineNameInternal = "caller-machine"; // machine
    private const string GrpcInternalCallerOsVersionInternal = "caller-os"; // system
    private const string GrpcInternalCallerSessionCodeInternal = "caller-vnnss"; // session
    private readonly string _grpcInternalCallerSessionCodeInternalValue = string.Empty;

    private readonly Metadata? _metadataHeaders;
    private readonly Metadata? _acceptMetadataHeaders;
    private readonly List<Metadata.Entry>? _metadataEntries;
    private readonly EGrpcEndUseType _endUseType;

    public GrpcHeaderConfig(EGrpcEndUseType endUseType, Dictionary<string, string>? headers = null)
    {
        var decomposeEndUseTypes = endUseType.GetFlags();
        if (decomposeEndUseTypes.Length > 1)
            throw new ArgumentException($"Only one flag can be set for {nameof(EGrpcEndUseType)}");

        endUseType = decomposeEndUseTypes.First();
        _metadataEntries ??= [];
        _endUseType = endUseType;
        if (_endUseType == EGrpcEndUseType.InternalServer)
        {
            _metadataHeaders = new Metadata();
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerUserNameInternal, Environment.UserName));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerMachineNameInternal, Environment.MachineName));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerOsVersionInternal,
                Environment.OSVersion.ToString()));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerSessionCodeInternal,
                _grpcInternalCallerSessionCodeInternalValue));
            _metadataEntries.ForEach(x => _metadataHeaders.Add(x));
            return;
        }

        if (_endUseType == EGrpcEndUseType.ExternalServer)
        {
            _metadataHeaders = new Metadata();
            _metadataEntries.AddRange(headers?.Select(header => new Metadata.Entry(header.Key, header.Value)) ?? []);
            _metadataEntries.ForEach(x => _metadataHeaders.Add(x));
            return;
        }

        if (_endUseType == EGrpcEndUseType.Client)
        {
            _acceptMetadataHeaders = new Metadata();
            _metadataEntries.AddRange(headers?.Select(header => new Metadata.Entry(header.Key, header.Value)) ?? []);
            _metadataEntries.ForEach(x => _acceptMetadataHeaders.Add(x));
        }
    }

    public void Replace(string key, string value)
    {
        if (_metadataHeaders == null)
            return;
        if (_endUseType == EGrpcEndUseType.InternalServer || _endUseType == EGrpcEndUseType.ExternalServer)
        {
            var callerUserEntry = _metadataHeaders.Get(key);
            if (callerUserEntry != null)
                _metadataHeaders.Remove(callerUserEntry);
            _metadataHeaders.Add(key, value);
            return;
        }

        if (_acceptMetadataHeaders == null)
            return;
        if (_endUseType == EGrpcEndUseType.Client)
        {
            var callerUserEntry = _acceptMetadataHeaders.Get(key);
            if (callerUserEntry != null)
                _acceptMetadataHeaders.Remove(callerUserEntry);
            _acceptMetadataHeaders.Add(key, value);
        }
    }

    public Metadata? GetHeader() => _metadataHeaders ?? _acceptMetadataHeaders;
}