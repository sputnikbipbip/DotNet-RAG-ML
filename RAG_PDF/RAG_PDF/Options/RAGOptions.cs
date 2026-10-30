namespace RAG_PDF.Options;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1/";
    public string EmbeddingModel { get; set; } = "embeddinggemma-2:270m";
    public string ChatModel { get; set; } = "phi4-mini:latest";
}

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public string CollectionName { get; set; } = "pdf-vectors-embeddinggemma2-768";
}
