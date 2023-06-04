using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Microsoft.eShopWeb.DeliveryOrderProcessor;

public static class DeliveryOrderProcessor
{
    [FunctionName("DeliveryOrderProcessor")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [CosmosDB(
            databaseName: "eShopOnWeb",
            containerName: "DeliveryOrders",
            Connection = "CosmosDBConnectionString",
            PartitionKey = "/id",
            CreateIfNotExists = true)] IAsyncCollector<dynamic> collector,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        using var reader = new StreamReader(req.Body);
        string requestBody = await reader.ReadToEndAsync();

        dynamic order = JsonConvert.DeserializeObject(requestBody);
        if (order is null)
        {
            return new BadRequestObjectResult("Unable to deserialize request body as JSON");
        }

        await collector.AddAsync(new
        {
            id = Guid.NewGuid().ToString(),
            order.ShipToAddress,
            order.OrderItems,
            order.FinalPrice
        });

        return new OkObjectResult("Order data successfully saved in Azure Cosmos DB");
    }
}
