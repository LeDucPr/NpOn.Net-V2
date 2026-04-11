namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;

public class ElasticSearchValueContainer
{
    public bool Status { get; set; }
    public string? RawJson { get; set; }
    public object? RawData { get; set; }

    public ElasticSearchValueContainer(bool status, string? rawJson = null, object? rawData = null)
    {
        Status = status;
        RawJson = rawJson;
        RawData = rawData;
    }
}
