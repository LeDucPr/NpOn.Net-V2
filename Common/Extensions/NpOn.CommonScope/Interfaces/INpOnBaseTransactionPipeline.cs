namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnBaseTransactionPipeline
{
    Task<INpOnBaseTransactionPipeline> Begin();    
    bool IsCompleted { get; } // break => false ( == Status)
    string? ErrorMessage { get; } 
}