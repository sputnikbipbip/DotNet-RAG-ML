using Microsoft.Extensions.VectorData;

namespace RAG_PDF.Models;

public class PdfChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string FileName { get; set; } = string.Empty;

    [VectorStoreVector(1536)] // Size for text-embedding-3-small
    public ReadOnlyMemory<float> Vector { get; set; }
}
