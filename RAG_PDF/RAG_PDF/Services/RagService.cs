using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using RAG_PDF.Models;
using System.Text;

namespace RAG_PDF.Services;

public class RagService
{
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly VectorStoreCollection<string, PdfChunk> _collection;

    public RagService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        VectorStoreCollection<string, PdfChunk> collection)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        _collection = collection;
    }

    public async Task<string> AskQuestionAsync(string question)
    {
        // 1. Generate embedding for the question
        var questionEmbeddings = await _embeddingGenerator.GenerateAsync([question]);
        var questionVector = questionEmbeddings[0].Vector;

        // 2. Search for relevant chunks
        // In v10, SearchAsync returns IAsyncEnumerable directly and takes searchValue, top, options
        var searchResults = _collection.SearchAsync(questionVector, 5);
        
        var contextBuilder = new StringBuilder();
        await foreach (var result in searchResults)
        {
            contextBuilder.AppendLine($"--- Source: {result.Record.FileName} ---");
            contextBuilder.AppendLine(result.Record.Text);
            contextBuilder.AppendLine();
        }

        string context = contextBuilder.ToString();

        // 3. Construct prompt and get answer
        var systemPrompt = "You are a helpful assistant that answers questions based on the provided PDF context. If the answer is not in the context, say you don't know.";
        var userPrompt = $"Context:\n{context}\n\nQuestion: {question}";

        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ]);

        return response.Text ?? "No answer generated.";
    }
}
