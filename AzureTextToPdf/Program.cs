using Azure.Storage.Blobs;
using AzureTextToPdf.Services;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BlobServiceClient>(provider => new BlobServiceClient(builder.Configuration.GetConnectionString("AzureBlobStorage")));
builder.Services.AddHostedService<FileProcessingService>();
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("ConnectionStrings:BlobStorage")));
builder.Services.AddSwaggerGen();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AzureTextToPdf v1");
    });
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();