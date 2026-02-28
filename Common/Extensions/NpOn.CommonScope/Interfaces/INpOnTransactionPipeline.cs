namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnTransactionPipeline
{
    void Begin();
    void Break();
    void SetFail();
    void SetFail(string exMess);
    void SetFail(Exception ex);
    void SetSuccess();
    bool IsCompleted { get; set; } // break => false ( == Status)
}