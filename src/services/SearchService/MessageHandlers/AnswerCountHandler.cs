using Contracts;
using Typesense;

namespace SearchService.MessageHandlers;

public class AnswerCountHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerCountUpdated message)
    {
        await client.UpdateDocument("questions", message.QuestionId,
            new { message.AnswerCount }
        );

        Console.WriteLine($"Updated answer count for question id {message.QuestionId} to {message.AnswerCount}.");
    }
}
