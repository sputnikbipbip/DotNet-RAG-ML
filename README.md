### Deep Dive into RAG (Retrieval-Augmented Generation)

This guide explains RAG and how to run this .NET application with local models and a local vector database. Ollama generates embeddings with **`embeddinggemma-2:270m`** and answers with **`phi4-mini:latest`**. Qdrant stores the document chunks and vectors. No Azure or DeepSeek account or API key is required.

---

### 1. Understanding RAG Concepts
**Retrieval-Augmented Generation (RAG)** is a technique used to give Large Language Models (LLMs) access to data they weren't trained on (like your private PDFs or internal documentation) without needing to retrain the model.

#### How it works (The Workflow):
1.  **Ingestion Phase**:
    *   **Extraction**: Text is pulled out of your documents (PDFs, docs, etc.).
    *   **Chunking**: The text is broken into smaller pieces (chunks). This is necessary because LLMs have a limited "context window."
    *   **Embedding**: Each chunk is passed through an **Embedding Model** which converts text into a long list of numbers (a **Vector**). Chunks with similar meanings have vectors that are "close" to each other mathematically.
    *   **Storage**: These vectors, along with the chunk text and filename, are stored in **Qdrant**, a local vector database.

2.  **Retrieval Phase**:
    *   When you ask a question, your question is also converted into a **Vector** using the same embedding model.
    *   The system searches the Vector Database for the chunks whose vectors are most similar to your question's vector.

3.  **Generation Phase**:
    *   The system takes the top relevant chunks and "stuffs" them into a prompt along with your question.
    *   **Prompt**: *"Using this context: [Found Chunks], please answer the following question: [Your Question]"*
    *   The local chat model uses the provided context to generate an answer grounded in the retrieved text.

---

### 2. Project Architecture & Implementation
This project is built with **ASP.NET Core 10** and uses the new `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData` abstractions.

#### Key Components:
*   **`PdfIngestionService.cs`**:
    *   Uses **iText** to extract text from PDF pages.
    *   Chunks text into 1000-character segments.
    *   Generates **768-dimensional** embeddings using Ollama and EmbeddingGemma 2, in batches of at most 16 chunks.
    *   Uses the model's document prefix, `title: none | text: `, for embedding inputs; stores the original chunk text.
    *   Saves the results into **Qdrant**, creating the collection on the first upload.
*   **`RagService.cs`**:
    *   Converts user questions into vectors using the same embedding model and its query prefix, `task: search result | query: `.
    *   Performs a cosine similarity search in Qdrant, retrieving up to five chunks.
    *   Sends the retrieved context + question to **Phi-4 Mini** through Ollama, limiting each answer to 512 output tokens.
*   **`Models/PdfChunk.cs`**:
    *   The data model for storage. Notice the `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes which define how the data maps to the vector database.
*   **`Program.cs`**:
    *   Configures Dependency Injection (DI) for the AI clients and vector store.
    *   Exposes `/upload` and `/ask` endpoints.

---

### 3. Prerequisites and Local Models
Install the **.NET 10 SDK**, **Docker with Docker Compose**, and [Ollama](https://ollama.com/download). Run Ollama directly on your machine; Compose runs Qdrant only.

Download the models once (skip this if they are already installed):

```bash
ollama pull embeddinggemma-2:270m
ollama pull phi4-mini:latest
ollama list
```

Ollama normally runs in the background at `http://localhost:11434`. If it is not already running, start `ollama serve` in another terminal. Do not start a second server if one already owns port 11434.

Verify the embedding model:

```bash
curl --fail-with-body http://localhost:11434/api/embed \
  -H "Content-Type: application/json" \
  -d '{"model":"embeddinggemma-2:270m","input":"title: none | text: Local document search."}'
```

The response should contain a **768-dimensional vector**. The model metadata's hidden-layer size is not necessarily its output embedding size. Ingestion and question retrieval must use the same embedding model.

The text-only embedding model is approximately 304 MB on disk; the Q4_K_M Phi-4 Mini download is approximately 2.5 GB. Runtime memory also includes model working memory and the chat context/KV cache, so download size is not a RAM estimate. Small embedding batches and bounded answer length keep requests modest. If necessary, configure Ollama with a modest context size (for example, `OLLAMA_CONTEXT_LENGTH=4096` in the server environment, then restart Ollama); the application does not request the model's full 128K context. See [Ollama's context documentation](https://docs.ollama.com/context-length).

---

### 4. Configuration
`RAG_PDF/RAG_PDF/appsettings.json` configures the local models and database:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434/v1/",
    "EmbeddingModel": "embeddinggemma-2:270m",
    "ChatModel": "phi4-mini:latest"
  },
  "Qdrant": {
    "Host": "localhost",
    "GrpcPort": 6334,
    "CollectionName": "pdf-vectors-embeddinggemma2-768"
  }
}
```

Optional environment-variable overrides:

```bash
export Ollama__BaseUrl="http://localhost:11434/v1/"
export Ollama__EmbeddingModel="embeddinggemma-2:270m"
export Ollama__ChatModel="phi4-mini:latest"
```

The application uses Ollama's OpenAI-compatible endpoints. The SDK requires a nonempty client key, so the application supplies the dummy value `ollama`, which the local server ignores. AzureOpenAI and DeepSeek settings are no longer read.

The schema in `Models/PdfChunk.cs` expects **768 dimensions**. If you change embedding models, review the model-specific prefixes in both services, update the schema if needed, choose a fresh Qdrant collection, and re-upload your PDFs. Even models with equal dimensions have different vector spaces. Changing only the chat model does not require re-uploading.

EmbeddingGemma's retrieval prefixes are documented in its [model prompt configuration](https://ollama.com/library/embeddinggemma-2:270m/blobs/031e56a498d3).

The .NET connector uses **gRPC on port 6334**, not the REST port 6333. You can override database settings with `Qdrant__Host`, `Qdrant__GrpcPort`, and `Qdrant__CollectionName`.

---

### 5. Running the Project
Run these commands from the repository root:

1. **Start Qdrant and wait for readiness**:
   ```bash
   docker compose up -d --wait
   docker compose ps
   ```
   The REST API is at `http://localhost:6333`; the dashboard is at `http://localhost:6333/dashboard`. Both published ports are bound to loopback for local development. This setup has no database authentication and is intended for use on your own machine.

2. **Start the application** with Ollama already running:
   ```bash
   dotnet run --project RAG_PDF/RAG_PDF/RAG_PDF.csproj --launch-profile http
   ```
   Open `http://localhost:5044` for the web interface. The development OpenAPI document is at `/openapi/v1.json`.

3. **Upload a PDF** through the web interface or with curl:
   ```bash
   curl --fail-with-body -F "file=@my-document.pdf" http://localhost:5044/upload
   ```

4. **Ask a question**:
   ```bash
   curl --fail-with-body -H "Content-Type: application/json" \
     -d '{"Question": "What is the summary of this document?"}' \
     http://localhost:5044/ask
   ```

**Re-upload existing PDFs:** the new `pdf-vectors-embeddinggemma2-768` collection starts empty. Existing collections (including `pdf-vectors`) are left intact, but their Azure-generated embeddings cannot be reused. Repeated uploads create additional chunks rather than replacing earlier uploads.

### 6. Persistence and Troubleshooting

- `docker compose down` stops/removes the container but retains document chunks and embeddings in the named `qdrant-storage` volume. Start again with `docker compose up -d --wait`.
- `docker compose down -v` **deletes the database volume**. You must re-upload PDFs afterward.
- Use `docker compose logs qdrant` and `curl --fail http://localhost:6333/readyz` to check the database.
- Upload a PDF before asking questions: the collection is created during ingestion.
- If ports 6333 or 6334 are already occupied, change the host-side Compose port mappings and update `Qdrant__GrpcPort` to match the mapped gRPC port.
- Check Ollama with `curl --fail http://localhost:11434/api/tags`; model-not-found errors require downloading the exact configured model tag. The first request can be slower while models load.
- A vector-dimension error means the configured embedding model and the 768-dimensional schema do not match. An existing collection with a different dimension also requires choosing a fresh collection.
- If using Rider, restart the application after changing configuration and set any overrides in its Run/Debug configuration.
- If you later containerize the application, use the Compose service name `qdrant` as its database host, rather than `localhost`.

### Summary of the "Best Way to Learn"
1.  **Read the Code**: Start with `PdfChunk.cs` to see how data is structured, then `PdfIngestionService.cs` for the flow.
2.  **Trace a Request**: Put a breakpoint in `RagService.AskQuestionAsync` and see how the search results look before they are sent to the LLM.
3.  **Experiment**: Change the `chunkSize` in `PdfIngestionService` and see how it affects the quality of the answers.
