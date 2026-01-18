using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using Grpc.Core;

namespace Common.Extensions.NpOn.HeaderConfig;

public class GrpcHeaderConfig : IHeaderConfig<Metadata, Metadata.Entry>
{
    // internal
    private const string GrpcInternalCallerUserName = DefaultHeaderConstant.GrpcInternalCallerUserName; // client
    private const string GrpcInternalCallerMachineName = DefaultHeaderConstant.GrpcInternalCallerMachineName; // machine
    private const string GrpcInternalCallerOsVersion = DefaultHeaderConstant.GrpcInternalCallerOsVersion; // system

    private const string GrpcInternalCallerSessionCode =
        DefaultHeaderConstant.GrpcInternalCallerSessionCode; // session (need override)/owner

    private readonly string _grpcInternalCallerSessionCodeValue =
        DefaultHeaderConstant.GrpcInternalCallerSessionCodeDefaultValue;


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
        if (_endUseType == EGrpcEndUseType.CallToInternalServer)
        {
            _metadataHeaders = new Metadata();
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerUserName, Environment.UserName));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerMachineName, Environment.MachineName));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerOsVersion,
                Environment.OSVersion.ToString()));
            _metadataEntries.Add(new Metadata.Entry(GrpcInternalCallerSessionCode,
                _grpcInternalCallerSessionCodeValue));
            _metadataEntries.ForEach(x => _metadataHeaders.Add(x));
            return;
        }

        if (_endUseType == EGrpcEndUseType.CallToExternalServer)
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
        if (_endUseType == EGrpcEndUseType.CallToInternalServer || _endUseType == EGrpcEndUseType.CallToExternalServer)
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
    public List<Metadata.Entry>? GetAllEntries() => _metadataEntries;
}