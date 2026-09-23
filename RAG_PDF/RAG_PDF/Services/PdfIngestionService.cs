using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using RAG_PDF.Models;
using System.Text;

namespace RAG_PDF.Services;

public class PdfIngestionService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly VectorStoreCollection<string, PdfChunk> _collection;

    public PdfIngestionService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        VectorStoreCollection<string, PdfChunk> collection)
    {
        _embeddingGenerator = embeddingGenerator;
        _collection = collection;
    }

    public async Task IngestPdfAsync(Stream pdfStream, string fileName)
    {
        // 1. Create index if not exists
        await _collection.EnsureCollectionExistsAsync();

        // 2. Extract text from PDF
        string text = ExtractTextFromPdf(pdfStream);

        // 3. Chunk text (Simple chunking for this example)
        var chunks = ChunkText(text, 1000);

        // 4. Generate embeddings and Save
        if (chunks.Count > 0)
        {
            // Generate embeddings for ALL chunks in a single (or internally batched) API call
            var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(chunks);

            // Zip the original chunks with their generated embeddings
            var pdfChunks = chunks.Zip(generatedEmbeddings, (text, embedding) => new PdfChunk
            {
                Text = text,
                FileName = fileName,
                Vector = embedding.Vector
            }).ToList();

            await _collection.UpsertAsync(pdfChunks);
        }
    }

    private string ExtractTextFromPdf(Stream pdfStream)
    {
        using var reader = new PdfReader(pdfStream);
        using var pdfDoc = new PdfDocument(reader);
        var sb = new StringBuilder();

        for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
        {
            var strategy = new SimpleTextExtractionStrategy();
            string pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(i), strategy);
            sb.AppendLine(pageText);
        }

        return sb.ToString();
    }

    private List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        for (int i = 0; i < text.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, text.Length - i);
            chunks.Add(text.Substring(i, length));
        }

        return chunks;
    }
}
