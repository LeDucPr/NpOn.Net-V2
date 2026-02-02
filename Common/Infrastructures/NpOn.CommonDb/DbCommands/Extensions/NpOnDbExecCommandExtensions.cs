using Newtonsoft.Json.Linq;

namespace Common.Infrastructures.NpOn.CommonDb.DbCommands.Extensions
{
    public static class NpOnDbExecCommandExtensions
    {
        public static INpOnDbExecFuncCommand AsFullJsonBlock(this INpOnDbExecFuncCommand execFuncCommand)
        {
            if (execFuncCommand.Params is not { Count: > 0 })
                return execFuncCommand;
            if (execFuncCommand.Params.Count == 1)
            {
                var singleParamValue = execFuncCommand.Params.Values.First();
                if (singleParamValue is JToken or System.Text.Json.JsonDocument)
                    return execFuncCommand;
            }

            var jsonObject = JObject.FromObject(execFuncCommand.Params);
            var firstValue = jsonObject.Properties().First().Value.ToString();
            var innerObject = JObject.Parse(firstValue);
            var newParams = new Dictionary<string, object>
            {
                { string.Empty, innerObject }
            };

            return new NpOnDbExecFuncCommand(
                execFuncCommand.DataBaseType,
                execFuncCommand.FuncName,
                newParams,
                execFuncCommand.AliasForSingleColumnOutput
            );
        }
    }
}