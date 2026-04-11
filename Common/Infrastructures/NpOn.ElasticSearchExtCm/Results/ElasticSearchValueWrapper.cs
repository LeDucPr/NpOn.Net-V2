using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;

public class ElasticSearchValueWrapper : NpOnWrapperResult<ElasticSearchValueContainer, string?>
{
    public ElasticSearchValueWrapper(ElasticSearchValueContainer parent) : base(parent)
    {
        if (parent.Status)
            SetSuccess();
        else
            SetFail("ElasticSearch operation failed");
    }

    protected override string? CreateResult()
    {
        return Parent.RawJson;
    }

    /// <summary>
    /// Gets the raw object returned from ElasticSearch (if available).
    /// </summary>
    public object? GetRawData() => Parent.RawData;

    /// <summary>
    /// Parses the JSON result into a model of type T using highly optimized System.Text.Json.
    /// </summary>
    public T? ToModel<T>()
    {
        if (!Status || string.IsNullOrWhiteSpace(Result))
            return default;

        return NetJsonMode.FromJson<T>(Result);
    }
    
    /// <summary>
    /// Parses the JSON result into a list of models of type T using highly optimized System.Text.Json.
    /// </summary>
    public IEnumerable<T>? ToModels<T>()
    {
         if (!Status || string.IsNullOrWhiteSpace(Result))
            return null;

         return NetJsonMode.FromJson<IEnumerable<T>>(Result);
    }
}
