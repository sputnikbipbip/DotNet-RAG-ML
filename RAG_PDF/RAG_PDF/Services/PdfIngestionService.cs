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
        foreach (var chunkText in chunks)
        {
            var embeddings = await _embeddingGenerator.GenerateAsync([chunkText]);
            var embedding = embeddings[0].Vector;
            
            var chunk = new PdfChunk
            {
                Text = chunkText,
                FileName = fileName,
                Vector = embedding
            };

            await _collection.UpsertAsync(chunk);
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
