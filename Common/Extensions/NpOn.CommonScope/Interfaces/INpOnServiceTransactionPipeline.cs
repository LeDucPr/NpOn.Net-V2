namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnServiceTransactionPipeline : INpOnBaseTransactionPipeline
{
    public INpOnBaseTransactionPipeline Register(Task<bool> addTask);
}