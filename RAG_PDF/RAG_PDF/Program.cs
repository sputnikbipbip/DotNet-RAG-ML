using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using OpenAI;
using RAG_PDF.Models;
using RAG_PDF.Options;
using RAG_PDF.Services;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure;
using Azure.Search.Documents.Indexes;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection("DeepSeek"));
builder.Services.Configure<AzureAISearchOptions>(builder.Configuration.GetSection("AzureAISearch"));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));

// DeepSeek Chat Client
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
    return new OpenAIClient(new ApiKeyCredential(options.ApiKey), new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl) })
        .GetChatClient(options.ModelId)
        .AsIChatClient();
});

// Azure OpenAI Embedding Generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
    return new AzureOpenAIClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey))
        .GetEmbeddingClient(options.EmbeddingDeploymentName)
        .AsIEmbeddingGenerator();
});

// Azure AI Search Vector Store
builder.Services.AddSingleton<VectorStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureAISearchOptions>>().Value;
    var indexClient = new SearchIndexClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
    return new AzureAISearchVectorStore(indexClient);
});

// Vector Store Collection
builder.Services.AddSingleton<VectorStoreCollection<string, PdfChunk>>(sp =>
{
    var vectorStore = sp.GetRequiredService<VectorStore>();
    var options = sp.GetRequiredService<IOptions<AzureAISearchOptions>>().Value;
    return vectorStore.GetCollection<string, PdfChunk>(options.IndexName);
});

// Services
builder.Services.AddScoped<PdfIngestionService>();
builder.Services.AddScoped<RagService>();

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

// API Endpoints
app.MapPost("/upload", async (IFormFile file, PdfIngestionService ingestionService) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("Invalid file.");
    
    using var stream = file.OpenReadStream();
    await ingestionService.IngestPdfAsync(stream, file.FileName);
    
    return Results.Ok($"File {file.FileName} ingested successfully.");
})
.DisableAntiforgery(); // Simplified for demo

app.MapPost("/ask", async (QuestionRequest request, RagService ragService) =>
{
    if (string.IsNullOrWhiteSpace(request.Question)) return Results.BadRequest("Question cannot be empty.");
    
    var answer = await ragService.AskQuestionAsync(request.Question);
    return Results.Ok(new { Answer = answer });
});

app.Run();

public record QuestionRequest(string Question);