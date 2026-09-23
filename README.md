### Deep Dive into RAG (Retrieval-Augmented Generation)

This guide explains the concept of RAG, how this project implements it using .NET, and how to set it up in Azure.

---

### 1. Understanding RAG Concepts
**Retrieval-Augmented Generation (RAG)** is a technique used to give Large Language Models (LLMs) access to data they weren't trained on (like your private PDFs or internal documentation) without needing to retrain the model.

#### How it works (The Workflow):
1.  **Ingestion Phase**:
    *   **Extraction**: Text is pulled out of your documents (PDFs, docs, etc.).
    *   **Chunking**: The text is broken into smaller pieces (chunks). This is necessary because LLMs have a limited "context window."
    *   **Embedding**: Each chunk is passed through an **Embedding Model** which converts text into a long list of numbers (a **Vector**). Chunks with similar meanings have vectors that are "close" to each other mathematically.
    *   **Storage**: These vectors, along with the original text, are stored in a **Vector Database** (like Azure AI Search).

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
    *   Uses **iTextSharp** to extract text from PDF pages.
    *   Chunks text into 1000-character segments.
    *   Generates embeddings using **Azure OpenAI**.
    *   Saves the results into **Azure AI Search**.
*   **`RagService.cs`**:
    *   Converts user questions into vectors.
    *   Performs a similarity search in Azure AI Search.
    *   Sends the retrieved context + question to the **DeepSeek Chat Client** to get the final answer.
*   **`Models/PdfChunk.cs`**:
    *   The data model for storage. Notice the `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes which define how the data maps to the vector database.
*   **`Program.cs`**:
    *   Configures Dependency Injection (DI) for the AI clients and vector store.
    *   Exposes `/upload` and `/ask` endpoints.

---

### 3. Azure Setup Guide
To make this project work, you need to provision three main resources in Azure.

#### Step A: Azure OpenAI (For Embeddings)
1.  Create an **Azure OpenAI** resource in the Azure Portal.
2.  Go to **Azure AI Foundry** (formerly OpenAI Studio) and deploy a model.
    *   **Model**: `text-embedding-3-small`
    *   **Deployment Name**: `text-embedding-3-small` (or whatever you prefer, but match it in `appsettings.json`).
3.  Copy the **Endpoint** and **API Key**.

#### Step B: Azure AI Search (Vector Database)
1.  Create an **Azure AI Search** resource.
2.  Choose the **Basic** tier or higher (Free tier has limitations for vector storage).
3.  Go to **Keys** and copy the **Primary Admin Key**.
4.  Copy the **Endpoint** (e.g., `https://your-search.search.windows.net`).

#### Step C: DeepSeek (The LLM/Reasoning Engine)
*   *Note: While you can use Azure OpenAI (GPT-4) for the chat part too, this project is configured to use **DeepSeek** as the brain.*
1.  Get an API Key from [DeepSeek's platform](https://platform.deepseek.com/).
2.  If you want to use Azure OpenAI for this part as well, you would need to modify `Program.cs` to use `AzureOpenAIClient.GetChatClient(...)`.

---

### 4. Configuration
Update your `appsettings.json` with the credentials obtained above:

```json
{
  "DeepSeek": {
    "ApiKey": "your_deepseek_key",
    "BaseUrl": "https://api.deepseek.com/v1",
    "ModelId": "deepseek-chat"
  },
  "AzureAISearch": {
    "Endpoint": "https://your-search-service.search.windows.net",
    "ApiKey": "your_search_admin_key",
    "IndexName": "pdf-vectors"
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your_azure_openai_key",
    "EmbeddingDeploymentName": "text-embedding-3-small"
  }
}
```

---

### 5. Running the Project
1.  **Start the Application**: Run the project in Rider or via `dotnet run`.
2.  **Upload a PDF**: Use the Swagger UI (at `/openapi/v1.json` or by opening `wwwroot/index.html` if implemented) or use `curl`:
    ```bash
    curl -X POST -F "file=@my-document.pdf" https://localhost:7053/upload
    ```
3.  **Ask a Question**:
    ```bash
    curl -X POST -H "Content-Type: application/json" -d '{"Question": "What is the summary of this document?"}' https://localhost:7053/ask
    ```

### Summary of the "Best Way to Learn"
1.  **Read the Code**: Start with `PdfChunk.cs` to see how data is structured, then `PdfIngestionService.cs` for the flow.
2.  **Trace a Request**: Put a breakpoint in `RagService.AskQuestionAsync` and see how the search results look before they are sent to the LLM.
3.  **Experiment**: Change the `chunkSize` in `PdfIngestionService` and see how it affects the quality of the answers.
