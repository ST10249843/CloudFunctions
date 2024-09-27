using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.AspNetCore.Mvc;

namespace QueueTest
{
    public class Function
    {
        private readonly ILogger<Function> _logger;

        public Function(ILogger<Function> logger)
        {
            _logger = logger;
        }

        [Function("RegisterUser")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Read request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string email = data?.email;
            string passwordHash = data?.passwordHash;

            // Validate the incoming data
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordHash))
            {
                return new BadRequestObjectResult("Invalid user data.");
            }

            // Create a new customer profile
            var customerProfile = new DynamicTableEntity
            {
                PartitionKey = email,
                RowKey = Guid.NewGuid().ToString(),
                Properties =
                {
                    { "Email", new EntityProperty(email) },
                    { "PasswordHash", new EntityProperty(passwordHash) }
                }
            };


            // Get the connection string from the environment variables
            string storageConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("CustomerProfiles");

            // Create the table if it doesn't exist
            await table.CreateIfNotExistsAsync();

            // Insert the new customer profile
            TableOperation insertOperation = TableOperation.Insert(customerProfile);
            await table.ExecuteAsync(insertOperation);

            _logger.LogInformation($"User {email} registered successfully.");
            return new OkObjectResult($"User {email} registered successfully.");
        }
    }
}
