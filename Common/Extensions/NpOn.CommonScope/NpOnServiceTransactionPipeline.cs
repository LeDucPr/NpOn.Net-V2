using Common.Extensions.NpOn.CommonScope.Interfaces;

namespace Common.Extensions.NpOn.CommonScope;

public class NpOnServiceTransactionPipeline : NpOnBaseTransactionPipeline, INpOnServiceTransactionPipeline
{
    private readonly List<Task<bool>> _tasks = new();

    public override async Task<INpOnBaseTransactionPipeline> Invoke()
    {
        await GeneralPipelineWrapper(async () =>
        {
            try
            {
                bool[] results = await Task.WhenAll(_tasks);
                return results.All(r => r);
                // if (results.Any(r => r == false))
                //     return false;
                // return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        });
        return this;
    }

    public INpOnBaseTransactionPipeline Register(Task<bool> addTask)
    {
        _tasks.Add(addTask);
        return this;
    }
}