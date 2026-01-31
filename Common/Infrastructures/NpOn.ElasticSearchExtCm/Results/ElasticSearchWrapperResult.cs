using System.Collections;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Elastic.Clients.Elasticsearch;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;

/// <summary>
/// A container class to wrap the ElasticSearch response.
/// </summary>
public class ElasticSearchContainer
{
    public object? SingleValue { get; }
    public IEnumerable<object>? Values { get; }
    public bool HasValue => IsSingle ? SingleValue != null : (Values?.Any() ?? false);
    public bool IsSingle { get; }

    public ElasticSearchContainer(object? value)
    {
        SingleValue = value;
        IsSingle = true;
    }

    public ElasticSearchContainer(IEnumerable<object>? values)
    {
        Values = values;
        IsSingle = false;
    }
}

/// <summary>
/// A generic wrapper for ElasticSearch results.
/// </summary>
public class ElasticSearchWrapperResult : NpOnWrapperResult<ElasticSearchContainer, IReadOnlyDictionary<string, INpOnCell>>, INpOnRowWrapper, INpOnTableWrapper
{
    public ElasticSearchWrapperResult(ElasticSearchContainer parent) : base(parent)
    {
        if (!parent.HasValue)
        {
            SetFail(EDbError.ElasticSearchResponseIsNull);
        }
        else
        {
            SetSuccess();
        }
    }

    protected override IReadOnlyDictionary<string, INpOnCell> CreateResult()
    {
        var valueToUse = Parent.IsSingle ? Parent.SingleValue : (Parent.Values?.FirstOrDefault());
        var cell = new NpOnCell<string?>(valueToUse?.ToString(), System.Data.DbType.String, "elasticsearch:json");
        return new Dictionary<string, INpOnCell> { { "value", cell } };
    }

    public T? As<T>()
    {
        var valueToUse = Parent.IsSingle ? Parent.SingleValue : (Parent.Values?.FirstOrDefault());
        if (valueToUse == null) return default;
        
        if (valueToUse is T tValue) return tValue;

        try 
        {
             // Assuming JSON serialization/deserialization might be needed or simple casting
             // For now, simple cast or convert
             return (T)Convert.ChangeType(valueToUse, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public T?[]? AsArray<T>()
    {
        if (Parent.IsSingle)
        {
            return Parent.SingleValue != null ? new[] { As<T>() } : Array.Empty<T?>();
        }

        return Parent.Values?.Select(v =>
        {
            if (v == null) return default;
            if (v is T tValue) return tValue;
            try
            {
                return (T)Convert.ChangeType(v, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }).ToArray();
    }

    // INpOnRowWrapper implementation
    public IReadOnlyDictionary<string, INpOnCell> GetRowWrapper()
    {
        return Result;
    }

    // INpOnTableWrapper implementation
    public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers =>
        Parent.Values?.Select((value, index) => new { value, index })
            .ToDictionary(
                item => item.index,
                item => (INpOnRowWrapper?)new ElasticSearchWrapperResult(new ElasticSearchContainer(item.value))
            ) ?? new Dictionary<int, INpOnRowWrapper?>();

    public INpOnCollectionWrapper CollectionWrappers => throw new NotImplementedException("Collection wrapper is not supported for ElasticSearch results yet.");
}