namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnBaseTransactionPipeline
{
    Task<INpOnBaseTransactionPipeline> Invoke();

    /// <summary>
    /// Invoke before call Next (if isNext = true)
    /// </summary>
    /// <param name="nextTransactionPipeline"></param>
    /// <param name="messageWhenError"></param>
    /// <param name="isNext"></param>
    /// <returns></returns>
    Task<INpOnBaseTransactionPipeline> Next(
        INpOnBaseTransactionPipeline? nextTransactionPipeline, bool isNext = false);

    bool IsCompleted { get; } // break => false ( == Status)
    string? ErrorMessage { get; }
}