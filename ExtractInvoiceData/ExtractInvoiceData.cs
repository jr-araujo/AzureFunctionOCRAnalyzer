using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace ExtractInvoiceData
{
    public class ExtractInvoiceData
    {
        [FunctionName("ExtractInvoiceData")]
        public async Task Run([BlobTrigger("invoices/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob,
                                     string name,
                                     [CosmosDB(databaseName: "Invoices",
                                               collectionName: "Items",
                                               CreateIfNotExists = true,
                                               ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<object> taskItems,
                                     ILogger log)
        {
            ComputerVisionClient client = Authenticate("https://computervisionctd.cognitiveservices.azure.com/",
                                                       "120dcf392a2d4c5bad40bb247a78d3eb");

            var extractedOcrResult = await ExtractAllTextFromAnImage(client, myBlob, log);

            await taskItems.AddAsync(new
            {
                OcrResult = extractedOcrResult,
                OcrText = TransformResultsToString(extractedOcrResult)
            });

            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }

        private static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
                new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
                {
                    Endpoint = endpoint
                };

            return client;
        }

        private static async Task<OcrResult> ExtractAllTextFromAnImage(
            ComputerVisionClient client,
            Stream file,
            ILogger log)
        {
            log.LogInformation("----------------------------------------------------------");
            log.LogInformation("OCR - LOCAL IMAGE");

            log.LogInformation($"Performing OCR stream...");

            // Get the recognized text
            OcrResult localFileOcrResult = await client.RecognizePrintedTextInStreamAsync(true, file, OcrLanguages.Unk);

            // Display text, language, angle, orientation, and regions of text from the results.
            log.LogInformation("Language: " + localFileOcrResult.Language);
            log.LogInformation("Text Angle: " + localFileOcrResult.TextAngle);
            log.LogInformation("Orientation: " + localFileOcrResult.Orientation);

            return localFileOcrResult;
            //}
        }

        private static string TransformResultsToString(OcrResult result)
        {
            return string.Join("\n",
                result.Regions.ToList().Select(region =>
                    string.Join(" ", region.Lines.ToList().Select(line =>
                         string.Join(" ", line.Words.ToList().Select(word =>
                             word.Text).ToArray())).ToArray())).ToArray());
        }
    }
}