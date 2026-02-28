namespace Common.Extensions.NpOn.CommonScope.Interfaces;

public interface INpOnTransactionScope
{
    bool Status { get; }
    // IEnumerable<INpOnTransactionPipeline> Pipeline 
}