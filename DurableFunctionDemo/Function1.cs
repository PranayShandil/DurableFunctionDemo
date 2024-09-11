using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.Net;

namespace DurableFunctionDemo
{
    public static class Function1
    {
        [Function(nameof(Function1))]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Function1));
            var outputs = new List<string>();

            try
            {
                logger.LogInformation("Saying hello.");

                // Replace name and input with values relevant for your Durable Functions Activity
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));
                outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Stockholm"));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred in the orchestrator function.");
                throw; // Re-throw the exception to ensure the orchestration fails
            }

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [Function(nameof(SayHello))]
        public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("SayHello");
            try
            {
                logger.LogInformation("Saying hello to {name}.", name);
                return $"Hello {name}!";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred in the SayHello activity.");
                throw; // Re-throw the exception to ensure the activity fails
            }
        }

        [Function("Function1_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Function1_HttpStart");

            try
            {
                // Function input comes from the request content.
                string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                    nameof(Function1));

                logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

                // Returns an HTTP 202 response with an instance management payload.
                // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
                //return client.CreateCheckStatusResponse(req, instanceId);

                // Manually create the response to avoid synchronous I/O operations
                var response = req.CreateResponse(HttpStatusCode.Accepted);
                var statusUri = new Uri($"{req.Url.GetLeftPart(UriPartial.Authority)}/runtime/webhooks/durabletask/instances/{instanceId}");
                var payload = new
                {
                    id = instanceId,
                    statusQueryGetUri = statusUri,
                    sendEventPostUri = $"{statusUri}/raiseEvent/{{eventName}}",
                    terminatePostUri = $"{statusUri}/terminate",
                    rewindPostUri = $"{statusUri}/rewind",
                    purgeHistoryDeleteUri = $"{statusUri}/purgeHistory"
                };

                await response.WriteAsJsonAsync(payload);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred in the HTTP start function.");
                var response = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await response.WriteStringAsync("An error occurred while starting the orchestration.");
                return response;
            }
        }
    }
}
