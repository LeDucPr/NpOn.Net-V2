namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnTransactionScope
{
    bool Status { get; }
    INpOnBaseTransactionPipeline? Next(); 
    IEnumerable<INpOnBaseTransactionPipeline> Pipelines { get; set; } 
}