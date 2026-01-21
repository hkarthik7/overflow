using Typesense;

namespace SearchService.Data;

public static class SearchInitializer
{
    public static async Task EnsureIndexExists(ITypesenseClient client)
    {
        const string schemaName = "questions";
        try
        {
            await client.RetrieveCollection(schemaName);
            Console.WriteLine($"Collection '{schemaName}' already exists.");
            return;
        }
        catch (TypesenseApiNotFoundException)
        {
            Console.WriteLine($"Collection '{schemaName}' not found. Creating...");
        }

        var schema = new Schema(schemaName,
        [
            new("id", FieldType.String),
            new("title", FieldType.String),
            new("content", FieldType.String),
            new("tags", FieldType.StringArray),
            new("createdAt", FieldType.Int64),
            new("hasAcceptedAnswer", FieldType.Bool),
            new("answerCount", FieldType.Int32)
        ])
        {
            DefaultSortingField = "createdAt"
        };

        await client.CreateCollection(schema);
        Console.WriteLine($"Collection '{schemaName}' created.");
    }
}
