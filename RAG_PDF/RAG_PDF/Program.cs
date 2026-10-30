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

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection("Ollama"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "Ollama:BaseUrl must be an absolute HTTP or HTTPS URL.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.EmbeddingModel), "Ollama:EmbeddingModel is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ChatModel), "Ollama:ChatModel is required.")
    .ValidateOnStart();
builder.Services.AddOptions<QdrantOptions>()
    .Bind(builder.Configuration.GetSection("Qdrant"))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Qdrant:Host is required.")
    .Validate(options => options.GrpcPort is > 0 and <= 65535, "Qdrant:GrpcPort must be between 1 and 65535.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CollectionName), "Qdrant:CollectionName is required.")
    .ValidateOnStart();

// Ollama's OpenAI-compatible API requires a nonempty client key but ignores its value.
builder.Services.AddSingleton<OpenAIClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return new OpenAIClient(new ApiKeyCredential("ollama"), new OpenAIClientOptions
    {
        Endpoint = new Uri(options.BaseUrl)
    });
});

// Local Ollama Chat Client
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return sp.GetRequiredService<OpenAIClient>()
        .GetChatClient(options.ChatModel)
        .AsIChatClient();
});

// Local Ollama Embedding Generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return sp.GetRequiredService<OpenAIClient>()
        .GetEmbeddingClient(options.EmbeddingModel)
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
