using Azure.Storage.Blobs;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.DeliveryOrderProcessor
{
    public class OrderItemsReserver
    {
        [FunctionName("OrderItemsReserver")]
        public async Task Run(
            [ServiceBusTrigger(
                "order-requests",
                Connection = "ServiceBusConnectionString")] string queueItem,
            [Blob(
                "order-requests/{rand-guid}.json",
                FileAccess.Write,
                Connection = "BlobStorageConnectionString")] BlobClient blobClient,
            ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {queueItem}");

            const int maxRetriesCount = 3;
            int retriesCount = 0;
            bool succeed = false;

            while (retriesCount < maxRetriesCount && !succeed)
            {
                try
                {
                    await UploadOrderRequestAsync(blobClient, queueItem);
                    succeed = true;
                }
                catch (Exception ex)
                {
                    retriesCount++;
                    log.LogError($"Order request upload failed. Attempt {retriesCount} of {maxRetriesCount}", ex);
                    if (retriesCount >= maxRetriesCount)
                    {
                        await SendFailureEmail(ex, log);
                    }
                }
            }
        }

        private async Task UploadOrderRequestAsync(BlobClient blobClient, string queueItem)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(queueItem);
            using MemoryStream stream = new MemoryStream(byteArray);
            await blobClient.UploadAsync(stream, overwrite: true);
        }

        private async Task SendFailureEmail(Exception exception, ILogger log)
        {
            string emailSenderUrl = Environment.GetEnvironmentVariable("EmailSenderUrl");

            var emailContent = new
            {
                subject = "OrderItemsReserver - Order Request Upload Failed",
                body = $"{exception.Message}: {exception.StackTrace}"
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsJsonAsync(emailSenderUrl, emailContent);
            if (response.IsSuccessStatusCode)
            {
                log.LogInformation("Failure Email was sent successfully");
            }
            else
            {
                log.LogError($"Failure Email was not sent successfully: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
    }
}
