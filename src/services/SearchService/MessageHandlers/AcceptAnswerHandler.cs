using Contracts;
using Typesense;

namespace SearchService.MessageHandlers;

public class AcceptAnswerHandler(ITypesenseClient client)
{
    public async Task HandleAsync(AnswerAccepted message)
    {
        await client.UpdateDocument("questions", message.QuestionId,
            new { hasAcceptedAnswer = true }
        );

        Console.WriteLine($"Answer accepted for question id {message.QuestionId}.");
    }
}
