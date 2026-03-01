namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnTransactionScope
{
    void SetFail();
    void SetFail(string exMess);
    void SetFail(Exception ex);
    void SetSuccess();
    bool Status { get; }
    INpOnBaseTransactionPipeline? Next(); 
    IEnumerable<INpOnBaseTransactionPipeline> Pipelines { get; set; } 
}