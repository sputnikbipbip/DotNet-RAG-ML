using Microsoft.Extensions.VectorData;

namespace RAG_PDF.Models;

public class PdfChunk
{
    public const int VectorDimensions = 768;

    [VectorStoreKey]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public string FileName { get; set; } = string.Empty;

    [VectorStoreVector(VectorDimensions, DistanceFunction = DistanceFunction.CosineSimilarity)] // embeddinggemma-2:270m
    public ReadOnlyMemory<float> Vector { get; set; }
}
