using Microsoft.Extensions.VectorData;

namespace RAG_PDF.Models;

public class PdfChunk
{
    [VectorStoreKey]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string FileName { get; set; } = string.Empty;

    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity)] // Size for text-embedding-3-small
    public ReadOnlyMemory<float> Vector { get; set; }
}
