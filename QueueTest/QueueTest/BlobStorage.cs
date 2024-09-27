using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs;  // Correct namespace for BlobTrigger
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace QueueTest
{
    public class BlobStorage
    {
        private readonly ILogger<BlobStorage> _logger;

        public BlobStorage(ILogger<BlobStorage> logger)
        {
            _logger = logger;
        }

        // Function that triggers when a blob is uploaded or updated in the 'multimedia' container
        [FunctionName(nameof(BlobStorage))]
        public async Task Run([BlobTrigger("multimedia/{name}", Connection = "AzureWebJobsStorage")] Stream blobStream, string name)
        {
            try
            {
                // Read the content from the blob
                using var blobStreamReader = new StreamReader(blobStream);
                var content = await blobStreamReader.ReadToEndAsync();

                // Log blob processing information
                _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content.Substring(0, Math.Min(100, content.Length))}...");

                // Optionally: You can also add logic to process the blob's data and store it in your system
                await ProcessBlobDataAsync(name, content);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing blob: {ex.Message}");
            }
        }

        private async Task ProcessBlobDataAsync(string blobName, string blobContent)
        {
            // Create a message object with the necessary details
            var message = new
            {
                BlobName = blobName,
                ProcessedTime = DateTime.UtcNow
            };

            string messageJson = JsonConvert.SerializeObject(message);

            // Assuming you have already set up a QueueClient for your 'cartqueue'
            var queueClient = new QueueClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"), "cartqueue");
            await queueClient.SendMessageAsync(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(messageJson)));

            _logger.LogInformation($"Message sent to queue for blob {blobName}");
        }
    }
}
