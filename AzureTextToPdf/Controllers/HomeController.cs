using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;
namespace AzureTextToPdf.Controllers;


[Controller]
public class HomeController : Controller
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueClient _queueClient;

    public HomeController(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _queueClient = new QueueClient(configuration.GetValue<string>("ConnectionStrings:BlobStorage"), "texttopdf");
    }

    public IActionResult Index()
    {

        return View();
    }

    [HttpPost("/upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return RedirectToAction("Index");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient("text-files");
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(file.FileName);

        using (var stream = file.OpenReadStream())
        {
            await blobClient.UploadAsync(stream, true);
        }

        await _queueClient.SendMessageAsync(file.FileName);

        return RedirectToAction("Index");
    }
}
