using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;
using OpenAI;
using RAG_PDF.Models;
using RAG_PDF.Options;
using RAG_PDF.Services;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<DeepSeekOptions>(builder.Configuration.GetSection("DeepSeek"));
builder.Services.AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection("Qdrant"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Qdrant:Host is required.")
    .Validate(options => options.GrpcPort is > 0 and <= 65535, "Qdrant:GrpcPort must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CollectionName), "Qdrant:CollectionName is required.")
    .ValidateOnStart();
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

// Local Qdrant Vector Store (gRPC)
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
    return new QdrantClient(options.Host, options.GrpcPort);
});
builder.Services.AddSingleton<VectorStore>(sp =>
{
    return new QdrantVectorStore(sp.GetRequiredService<QdrantClient>(), ownsClient: false);
});

// Vector Store Collection
builder.Services.AddSingleton<VectorStoreCollection<Guid, PdfChunk>>(sp =>
{
    var vectorStore = sp.GetRequiredService<VectorStore>();
    var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
    return vectorStore.GetCollection<Guid, PdfChunk>(options.CollectionName);
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
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("Invalid file.");
    }
    
    using var stream = file.OpenReadStream();
    await ingestionService.IngestPdfAsync(stream, file.FileName);
    
    return Results.Ok($"File {file.FileName} ingested successfully.");
})
.DisableAntiforgery(); // Simplified for demo

app.MapPost("/ask", async (QuestionRequest request, RagService ragService) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        return Results.BadRequest("Question cannot be empty.");
    }
    
    var answer = await ragService.AskQuestionAsync(request.Question);
    return Results.Ok(new { Answer = answer });
});

app.Run();
