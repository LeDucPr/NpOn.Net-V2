using Common.Extensions.NpOn.CommonScope.Interfaces;

namespace Common.Extensions.NpOn.CommonScope;

public class NpOnPipelineScope
{
    private readonly List<INpOnBaseTransactionPipeline> _pipelines = [];

    // public NpOnPipelineScope()
    // {
    // }

    public NpOnPipelineScope(INpOnBaseTransactionPipeline initPipeline)
    {
        _pipelines.Add(initPipeline);
    }

    public INpOnBaseTransactionPipeline? Current()
    {
        return _pipelines.LastOrDefault();
    }

    /// <summary>
    /// Gọi Next sẽ Invoke Pipeline hiện tại và trở tiếp tới pipeline sau 
    /// </summary>
    /// <param name="nextPipeline"></param>
    /// <returns></returns>
    public async Task<INpOnBaseTransactionPipeline?> Next(INpOnBaseTransactionPipeline? nextPipeline/*, string? messageWhenError = null*/)
    {
        await (_pipelines.LastOrDefault()?.Next(nextPipeline, true) ?? Task.CompletedTask); // the last invoked 
        if (nextPipeline != null)
            _pipelines.Add(nextPipeline);
        return nextPipeline;
    }
}