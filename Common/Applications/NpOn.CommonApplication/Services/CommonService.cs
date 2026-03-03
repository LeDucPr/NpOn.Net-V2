using System.Runtime.CompilerServices;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonScope;

namespace Common.Applications.NpOn.CommonApplication.Services;

public class CommonService(ILogger<CommonService> logger)
{
    // multi transactions (INpOnBaseTransactionPipeline) contains any object inherits it
    protected async Task<CommonResponse> CommonProcess(
        Func<CommonResponse, NpOnPipelineScope?, Task> processFunc,
        [CallerMemberName] string callerName = "") // Compiler func call stack
    {
        CommonResponse response = new CommonResponse();
        NpOnPipelineScope pipelineScope = new NpOnPipelineScope(new NpOnServiceTransactionPipeline());
        try
        {
            await processFunc(response, pipelineScope);
            if (pipelineScope.Current() is { IsCompleted: false }) // pipeline != null && !pipeline.IsCompleted
            {
                response.SetFail($"Break in pipeline in Func: {callerName}");
                return response;
            }
        }
        catch (Exception e)
        {
            response.SetFail($"An unexpected error occurred in {callerName}: {e.Message}");
            logger.LogError(e, "An error occurred in CommonProcess (called by {Caller}): {ErrorMessage}", callerName,
                e.Message);
        }

        return response;
    }

    protected async Task<CommonResponse<T>> CommonProcess<T>(
        Func<CommonResponse<T>, NpOnPipelineScope, Task> processFunc,
        [CallerMemberName] string callerName = "") // Compiler func call stack
    {
        CommonResponse<T> response = new CommonResponse<T>();
        NpOnPipelineScope pipelineScope = new NpOnPipelineScope(new NpOnServiceTransactionPipeline());
        try
        {
            await processFunc(response, pipelineScope);
            if (pipelineScope.Current() is { IsCompleted: false }) // pipeline != null && !pipeline.IsCompleted
            {
                response.SetFail($"Break in pipeline in Func: {callerName}");
                return response;
            }
        }
        catch (Exception e)
        {
            response.SetFail($"An unexpected error occurred: {e.Message}");
            logger.LogError(e, "An error occurred in CommonProcess: {ErrorMessage}", e.Message);
        }

        return response;
    }


    // process 
    protected async Task<CommonResponse<T>> CommonProcess<T>(
        params Func<CommonResponse<T>, Task<(CommonResponse<T> response, EControlFlow flow)>>[] processFunctions)
    {
        CommonResponse<T> response = new CommonResponse<T>();
        try
        {
            foreach (var processFunc in processFunctions)
            {
                var (resp, flow) = await processFunc(response);
                response = resp;
                if (!resp.Status)
                    break;
                if (flow == EControlFlow.Continue)
                    continue;
                if (flow == EControlFlow.Break)
                    break;
            }
        }
        catch (Exception e)
        {
            response.SetFail($"An unexpected error occurred: {e.Message}");
            logger.LogError(e, "An error occurred in CommonProcess: {ErrorMessage}", e.Message);
        }

        return response;
    }

    protected async Task<CommonResponse<T>> CommonProcess<T>(Func<CommonResponse<T>, Task> processFunc)
    {
        CommonResponse<T> response = new CommonResponse<T>();
        try
        {
            await processFunc(response);
        }
        catch (Exception e)
        {
            response.SetFail($"An unexpected error occurred: {e.Message}");
            logger.LogError(e, "An error occurred in CommonProcess: {ErrorMessage}", e.Message);
        }

        return response;
    }

    protected async Task<CommonResponse> CommonProcess(Func<CommonResponse, Task> processFunc)
    {
        CommonResponse response = new CommonResponse();
        try
        {
            await processFunc(response);
        }
        catch (Exception e)
        {
            response.SetFail($"An unexpected error occurred: {e.Message}");
            logger.LogError(e, "An error occurred in CommonProcess: {ErrorMessage}", e.Message);
        }

        return response;
    }
}