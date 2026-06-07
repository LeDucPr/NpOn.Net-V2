using System;
using System.Collections.Generic;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Results;

public class ZeroMqResultSetWrapper : NpOnWrapperResult, INpOnRowWrapper, INpOnTableWrapper
{
    private string? _payload;

    public string? Payload => _payload;
    public string? ErrorMessage { get; set; }

    // Delegate to return this object to the pool
    public Action<ZeroMqResultSetWrapper>? ReturnToPool { get; set; }

    public ZeroMqResultSetWrapper()
    {
        // Default constructor for object pooling
    }

    public ZeroMqResultSetWrapper(string payload)
    {
        Init(payload);
    }

    public ZeroMqResultSetWrapper Init(string payload)
    {
        _payload = payload;
        SetSuccess();
        return this;
    }

    public void Reset()
    {
        _payload = null;
        ErrorMessage = null;
        // Base class NpOnWrapperResult doesn't expose a direct Reset(), but calling Init() or SetFail() resets its state.
    }

    // INpOnRowWrapper
    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper()
    {
        var cell = new NpOnCell<string?>(_payload, System.Data.DbType.String, "zeromq:string");
        return new Dictionary<string, INpOnCell> { { "value", cell } };
    }

    // INpOnTableWrapper
    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers =>
        new Dictionary<int, INpOnRowWrapper?> { { 0, this } };

    public INpOnCollectionWrapper CollectionWrappers =>
        throw new NotImplementedException("Collection wrapper is not supported for ZeroMQ.");

    public override void Dispose()
    {
        ReturnToPool?.Invoke(this);
    }
}