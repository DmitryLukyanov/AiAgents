using Microsoft.SemanticKernel;

namespace AiAgents.Filters
{
    public sealed class EarlyPluginChainTerminationFilter : IAutoFunctionInvocationFilter
    {
        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            // Call the function first.
            await next(context);

            //// Get a function result from context.
            var result = context.Result.GetValue<string>();

            //// If the result meets the condition, terminate the process.
            //// Otherwise, the function calling process will continue.
            //if (result == "condition")
            //{
            //    context.Terminate = true;
            //}
        }
    }
}
