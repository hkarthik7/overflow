using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;

namespace QuestionService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionsController(QuestionDbContext db) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto questionDto)
        {
            await ValidateTags(questionDto);

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
            var question =  await db.Questions.FindAsync(id);

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

            await ValidateTags(questionDto);

            question.Title = questionDto.Title;
            question.Content = questionDto.Content;
            question.TagSlugs = questionDto.Tags;
            question.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

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

            return NoContent();
        }

        private async Task<BadRequestObjectResult?> ValidateTags(CreateQuestionDto questionDto)
        {
            var validTags = await db.Tags
                .Where(t => questionDto.Tags.Contains(t.Slug))
                .ToListAsync();

            var missing = questionDto.Tags.Except([.. validTags.Select(x => x.Slug)]).ToList();

            if (missing.Count != 0)
                return BadRequest($"Invalid tags: {string.Join(", ", missing)}");
            
            return null;
        }
    }
}
