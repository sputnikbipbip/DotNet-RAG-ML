using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RAG_PDF.Options;

namespace RAG_PDF.Services;

public class DocumentListingService(QdrantClient client, IOptions<QdrantOptions> options)
{
    public async Task<IReadOnlyList<string>> ListFileNamesAsync(CancellationToken cancellationToken)
    {
        var collectionName = options.Value.CollectionName;
        if (!await client.CollectionExistsAsync(collectionName, cancellationToken))
        {
            return [];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        PointId? offset = null;
        do
        {
            var page = await client.ScrollAsync(collectionName, limit: 256, offset: offset,
                payloadSelector: new[] { "FileName" }, vectorsSelector: false, cancellationToken: cancellationToken);
            foreach (var point in page.Result)
            {
                if (point.Payload.TryGetValue("FileName", out var fileName) &&
                    fileName.KindCase == Value.KindOneofCase.StringValue &&
                    !string.IsNullOrWhiteSpace(fileName.StringValue))
                {
                    names.Add(fileName.StringValue);
                }
            }
            offset = page.NextPageOffset;
        } while (offset is not null);

        return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
