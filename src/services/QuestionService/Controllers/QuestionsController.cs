using System.Security.Claims;
using Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.Services;
using Wolverine;

namespace QuestionService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionsController(QuestionDbContext db, IMessageBus bus, TagService tagService) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto questionDto)
        {
            if (!await tagService.AreTagsValidAsync(questionDto.Tags))
            {
                return BadRequest("One or more tags are invalid.");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");

            if (userId is null || name is null) return BadRequest("Cannot get user details");

            var question = new Question
            {
                Title = questionDto.Title,
                Content = questionDto.Content,
                TagSlugs = questionDto.Tags,
                AskerDisplayName = name,
                AskerId = userId,
            };

            db.Questions.Add(question);
            await db.SaveChangesAsync();

            await bus.PublishAsync(new QuestionCreated(question.Id, question.Title,
                question.Content, question.CreatedAt, question.TagSlugs));

            return Created($"/questions/{question.Id}", question);
        }

        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
        {
            var query = db.Questions.AsQueryable();

            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(q => q.TagSlugs.Contains(tag));
            }

            return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(string id)
        {
            var question = await db.Questions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (question is null)
                return NotFound();

            await db.Questions.Where(x => x.Id == id)
                .ExecuteUpdateAsync(q => q.SetProperty(x => x.ViewCount,
                    x => x.ViewCount + 1));

            return question;
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto questionDto)
        {
            var question = await db.Questions.FindAsync(id);

            if (question is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
                return Forbid();

            if (!await tagService.AreTagsValidAsync(questionDto.Tags))
            {
                return BadRequest("One or more tags are invalid.");
            }

            question.Title = questionDto.Title;
            question.Content = questionDto.Content;
            question.TagSlugs = questionDto.Tags;
            question.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title,
                question.Content, [.. question.TagSlugs]));

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);

            if (question is null)
                return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (question.AskerId != userId)
                return Forbid();

            db.Questions.Remove(question);
            await db.SaveChangesAsync();

            await bus.PublishAsync(new QuestionDeleted(question.Id));

            return NoContent();
        }

        [Authorize]
        [HttpPost("{questionId}/answers")]
        public async Task<ActionResult<Answer>> PostAnswer(string questionId, CreateAnswerDto answerDto)
        {
            var question = await db.Questions.FindAsync(questionId);

            if (question is null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");

            if (userId is null || name is null) return BadRequest("Cannot get user details");

            var answer = new Answer
            {
                Content = answerDto.Content,
                UserId = userId,
                UserDisplayName = name,
                QuestionId = questionId
            };

            question.Answers.Add(answer);
            question.AnswerCount++;

            await db.SaveChangesAsync();

            await bus.PublishAsync(new AnswerCountUpdated(questionId, question.AnswerCount));

            return Created($"/questions/{questionId}", answer);
        }

        [Authorize]
        [HttpPut("{questionId}/answers/{answerId}")]
        public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto answerDto)
        {
            var answer = await db.Answers.FindAsync(answerId);

            if (answer is null) return NotFound();

            if (answer.QuestionId != questionId) return BadRequest("Cannot update the answer details");

            answer.Content = answerDto.Content;
            answer.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{questionId}/answers/{answerId}")]
        public async Task<ActionResult> DeleteAnswer(string questionId, string answerId)
        {
            var answer = await db.Answers.FindAsync(answerId);
            var question = await db.Questions.FindAsync(questionId);

            if (answer is null || question is null)
                return NotFound();

            if (answer.QuestionId != questionId || answer.Accepted)
                return BadRequest("Cannot delete the answer");

            db.Answers.Remove(answer);
            question.AnswerCount--;

            await db.SaveChangesAsync();

            await bus.PublishAsync(new AnswerCountUpdated(questionId, question!.AnswerCount));

            return NoContent();
        }

        [Authorize]
        [HttpPost("{questionId}/answers/{answerId}/accept")]
        public async Task<ActionResult> AcceptAnswer(string questionId, string answerId)
        {
            var answer = await db.Answers.FindAsync(answerId);
            var question = await db.Questions.FindAsync(questionId);

            if (answer is null || question is null)
                return NotFound();

            if (answer.QuestionId != questionId || question.HasAcceptedAnswer)
                return BadRequest("Cannot accept the answer for this question");

            answer.Accepted = true;
            question.HasAcceptedAnswer = true;

            await db.SaveChangesAsync();

            await bus.PublishAsync(new AnswerAccepted(questionId));

            return NoContent();
        }
    }
}
