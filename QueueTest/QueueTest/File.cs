using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;

namespace QueueTest
{
    public class File
    {
        private readonly ILogger<File> _logger;

        public File(ILogger<File> logger)
        {
            _logger = logger;
        }

        [Function("File")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Handle POST request to log login attempts
            if (req.Method == HttpMethods.Post)
            {
                // Read request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);

                string email = data?.email;

                // Validate the incoming data
                if (string.IsNullOrWhiteSpace(email))
                {
                    return new BadRequestObjectResult("Invalid user email.");
                }

                // Log the login attempt
                await LogLoginAttemptAsync(email);
                return new OkObjectResult($"User {email} logged in successfully.");
            }

            // Handle GET request
            return new OkObjectResult("Welcome to Azure Functions!");
        }

        private async Task LogLoginAttemptAsync(string userEmail)
        {
            // Initialize Azure File Share client
            string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage"); // Get connection string from environment variables
            string shareName = "logreport"; // Your file share name
            var shareClient = new ShareClient(connectionString, shareName);
            var fileClient = shareClient.GetRootDirectoryClient().GetFileClient("login-log.txt");

            // Create log entry
            var logEntry = $"{DateTime.UtcNow}: User {userEmail} logged in.\n";
            byte[] logBytes = Encoding.UTF8.GetBytes(logEntry);

            using (var stream = new MemoryStream(logBytes))
            {
                // Read existing content
                var existingContent = await ReadExistingContentAsync(fileClient);

                // Combine existing content with new log entry
                using (var combinedStream = new MemoryStream())
                {
                    if (existingContent != null)
                    {
                        existingContent.CopyTo(combinedStream);
                    }
                    stream.CopyTo(combinedStream);
                    combinedStream.Position = 0;

                    // Upload combined content
                    await UploadContentAsync(fileClient, combinedStream);
                }
            }
        }

        private async Task<Stream> ReadExistingContentAsync(ShareFileClient fileClient)
        {
            try
            {
                var response = await fileClient.DownloadAsync();
                return response.Value.Content;
            }
            catch
            {
                return null; // Handle exceptions (file may not exist)
            }
        }

        private async Task UploadContentAsync(ShareFileClient fileClient, Stream contentStream)
        {
            // Create or overwrite the file
            await fileClient.CreateAsync(contentStream.Length);  // Ensure the file exists
            contentStream.Position = 0;
            await fileClient.UploadAsync(contentStream); // Upload the content
        }
    }
}
