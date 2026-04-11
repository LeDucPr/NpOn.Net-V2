namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Results;

public class ElasticSearchValueContainer
{
    public bool Status { get; set; }
    public string? RawJson { get; set; }
    public object? RawData { get; set; }

    // Execution metadata
    public ElasticSearchValueShardStatistics? Shards { get; set; }
    public long? Took { get; set; } // ES processing time in ms

    public ElasticSearchValueContainer(bool status, string? rawJson = null, object? rawData = null)
    {
        Status = status;
        RawJson = rawJson;
        RawData = rawData;
    }
}

public class ElasticSearchValueShardStatistics
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}