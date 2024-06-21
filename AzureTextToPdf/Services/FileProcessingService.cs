using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using System.Text;
namespace AzureTextToPdf.Services;

public class FileProcessingService : BackgroundService
{
    private readonly ILogger<FileProcessingService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueClient _queueClient;

    public FileProcessingService(ILogger<FileProcessingService> logger, BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _queueClient = new QueueClient(configuration.GetValue<string>("ConnectionStrings:BlobStorage"), "texttopdf");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessQueueMessagesAsync(stoppingToken);
            await Task.Delay(10000, stoppingToken);
        }
    }

    private async Task ProcessQueueMessagesAsync(CancellationToken stoppingToken)
    {
        QueueMessage[] messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 10, cancellationToken: stoppingToken);

        foreach (var message in messages)
        {
            try
            {
                string fileName = message.MessageText;
                await ProcessFileAsync(fileName, stoppingToken);
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message with ID: {MessageId}", message.MessageId);
            }
        }
    }

    private async Task ProcessFileAsync(string fileName, CancellationToken stoppingToken)
    {
        var textBlobContainer = _blobServiceClient.GetBlobContainerClient("text-files");
        await textBlobContainer.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        var textBlobClient = textBlobContainer.GetBlobClient(fileName);
        var pdfBlobContainer = _blobServiceClient.GetBlobContainerClient("pdf-files");
        await pdfBlobContainer.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        var pdfBlobClient = pdfBlobContainer.GetBlobClient(Path.ChangeExtension(fileName, ".pdf"));

        using var memoryStream = new MemoryStream();
        await textBlobClient.DownloadToAsync(memoryStream, stoppingToken);
        string textContent = Encoding.UTF8.GetString(memoryStream.ToArray());

        using var pdfStream = new MemoryStream();
        var document = new PdfDocument();
        var page = document.AddPage();
        var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Verdana", 12); 
        var textFormatter = new XTextFormatter(graphics);

        var rect = new XRect(40, 40, page.Width - 80, page.Height - 80);
        graphics.DrawRectangle(XBrushes.Transparent, rect); 

        textFormatter.DrawString(textContent, font, XBrushes.Black, rect, XStringFormats.TopLeft);

        document.Save(pdfStream, false);
        pdfStream.Position = 0; 

        await pdfBlobClient.UploadAsync(pdfStream, true, stoppingToken);
        _logger.LogInformation("Uploaded PDF for '{FileName}'", fileName);
    }
}