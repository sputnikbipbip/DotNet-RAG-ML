namespace RAG_PDF.Options;

public class DeepSeekOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";
    public string ModelId { get; set; } = "deepseek-chat";
}

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public string CollectionName { get; set; } = "pdf-vectors";
}

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-small";
}
