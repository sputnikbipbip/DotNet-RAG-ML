### Deep Dive into RAG (Retrieval-Augmented Generation)

This guide explains RAG, how this project implements it using .NET, and how to run it with a local Qdrant database. Azure OpenAI generates embeddings and DeepSeek generates answers; only vector storage runs locally.

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
    *   The LLM (like DeepSeek or GPT-4) uses the provided context to generate a factual, grounded answer.

---

### 2. Project Architecture & Implementation
This project is built with **ASP.NET Core 10** and uses the new `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData` abstractions.

#### Key Components:
*   **`PdfIngestionService.cs`**:
    *   Uses **iText** to extract text from PDF pages.
    *   Chunks text into 1000-character segments.
    *   Generates embeddings using **Azure OpenAI**.
    *   Saves the results into **Qdrant**, creating the collection on the first upload.
*   **`RagService.cs`**:
    *   Converts user questions into vectors.
    *   Performs a cosine similarity search in Qdrant, retrieving up to five chunks.
    *   Sends the retrieved context + question to the **DeepSeek Chat Client** to get the final answer.
*   **`Models/PdfChunk.cs`**:
    *   The data model for storage. Notice the `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes which define how the data maps to the vector database.
*   **`Program.cs`**:
    *   Configures Dependency Injection (DI) for the AI clients and vector store.
    *   Exposes `/upload` and `/ask` endpoints.

---

### 3. Prerequisites and Model Providers
Install the **.NET 10 SDK** and **Docker with Docker Compose**. You also need an Azure OpenAI embedding deployment and a DeepSeek API key. Azure AI Search is no longer required.

#### Step A: Azure OpenAI (For Embeddings)
1.  Create an **Azure OpenAI** resource in the Azure Portal.
2.  Go to **Azure AI Foundry** (formerly OpenAI Studio) and deploy a model.
    *   **Model**: `text-embedding-3-small`
    *   **Deployment Name**: `text-embedding-3-small` (or whatever you prefer, but match it in configuration).
3.  Copy the **Endpoint** and **API Key**.

The vector schema expects **1,536 dimensions**. Ingestion and question retrieval must use the same embedding model. If you change the model or dimensions, update the schema as needed, use a new collection, and re-upload your PDFs.

#### Step B: DeepSeek (The LLM/Reasoning Engine)
1.  Get an API Key from [DeepSeek's platform](https://platform.deepseek.com/).

---

### 4. Configuration
`RAG_PDF/RAG_PDF/appsettings.json` configures the local database:

```json
{
  "Qdrant": {
    "Host": "localhost",
    "GrpcPort": 6334,
    "CollectionName": "pdf-vectors"
  }
}
```

Set model-provider credentials with environment variables in the terminal where you run the application:

```bash
export AzureOpenAI__Endpoint="https://your-resource.openai.azure.com/"
export AzureOpenAI__ApiKey="your_azure_openai_key"
export AzureOpenAI__EmbeddingDeploymentName="text-embedding-3-small"
export DeepSeek__ApiKey="your_deepseek_key"
```

The existing DeepSeek defaults are `https://api.deepseek.com/v1` and `deepseek-chat`. Environment variables override appsettings; keep actual credentials out of committed configuration.

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

2. **Start the application** after setting the environment variables above:
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

**Re-upload existing PDFs:** the new Qdrant collection starts empty; existing Azure AI Search records are not migrated. Embeddings are regenerated during upload. Repeated uploads create additional chunks rather than replacing earlier uploads.

### 6. Persistence and Troubleshooting

- `docker compose down` stops/removes the container but retains document chunks and embeddings in the named `qdrant-storage` volume. Start again with `docker compose up -d --wait`.
- `docker compose down -v` **deletes the database volume**. You must re-upload PDFs afterward.
- Use `docker compose logs qdrant` and `curl --fail http://localhost:6333/readyz` to check the database.
- Upload a PDF before asking questions: the collection is created during ingestion.
- If ports 6333 or 6334 are already occupied, change the host-side Compose port mappings and update `Qdrant__GrpcPort` to match the mapped gRPC port.
- A vector-dimension error means the embedding deployment and the 1,536-dimensional schema do not match.
- If you later containerize the application, use the Compose service name `qdrant` as its database host, rather than `localhost`.

### Summary of the "Best Way to Learn"
1.  **Read the Code**: Start with `PdfChunk.cs` to see how data is structured, then `PdfIngestionService.cs` for the flow.
2.  **Trace a Request**: Put a breakpoint in `RagService.AskQuestionAsync` and see how the search results look before they are sent to the LLM.
3.  **Experiment**: Change the `chunkSize` in `PdfIngestionService` and see how it affects the quality of the answers.
