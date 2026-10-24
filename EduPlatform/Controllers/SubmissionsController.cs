using Dapper;
using EduPlatform.Helpers;
using EduPlatform.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace EduPlatform.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class SubmissionsController : ControllerBase
{
    private readonly IDbConnection _db;

    public SubmissionsController(IDbConnection db)
    {
        _db = db;
    }

    [Authorize(Roles = "Student")]
    [HttpPost("assignments/{assignmentId:int}/submit")]
    public async Task<IActionResult> Submit(int assignmentId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Файл решения обязателен" });
        }

        var assignment = await FindAssignment(assignmentId);
        if (assignment == null)
        {
            return NotFound(new { message = "Задание не найдено" });
        }

        var studentId = User.GetUserId();
        if (!await IsEnrolled(assignment.CourseId, studentId))
        {
            return Forbid();
        }

        if (assignment.DueDate.HasValue && DateTime.UtcNow > assignment.DueDate.Value)
        {
            return BadRequest(new { message = "Срок сдачи задания истёк" });
        }

        var existing = await _db.QueryFirstOrDefaultAsync<SubmissionRow>(
            "SELECT Id, Grade, StoredFileName FROM Submissions WHERE AssignmentId = @assignmentId AND StudentId = @studentId",
            new { assignmentId, studentId });

        if (existing != null && existing.Grade != null)
        {
            return Conflict(new { message = "Работа уже оценена, повторная сдача недоступна" });
        }

        var storedName = await FileStorage.SaveAsync(file);

        if (existing == null)
        {
            const string insertSql = """
                INSERT INTO Submissions (AssignmentId, StudentId, OriginalFileName, StoredFileName)
                VALUES (@assignmentId, @studentId, @OriginalFileName, @StoredFileName);
                SELECT CAST(SCOPE_IDENTITY() AS INT);
                """;

            var id = await _db.ExecuteScalarAsync<int>(insertSql, new
            {
                assignmentId,
                studentId,
                OriginalFileName = file.FileName,
                StoredFileName = storedName
            });

            return Ok(await FindView(id));
        }

        await _db.ExecuteAsync("""
            UPDATE Submissions
            SET OriginalFileName = @OriginalFileName,
                StoredFileName = @StoredFileName,
                SubmittedAt = GETUTCDATE()
            WHERE Id = @Id
            """, new
        {
            existing.Id,
            OriginalFileName = file.FileName,
            StoredFileName = storedName
        });

        return Ok(await FindView(existing.Id));
    }

    [Authorize(Roles = "Teacher")]
    [HttpGet("assignments/{assignmentId:int}/submissions")]
    public async Task<IActionResult> List(int assignmentId)
    {
        if (await FindAssignment(assignmentId) == null)
        {
            return NotFound();
        }

        const string sql = """
            SELECT s.Id, s.AssignmentId, s.StudentId, u.username AS StudentName,
                   s.OriginalFileName, s.SubmittedAt, s.Grade, s.Feedback, s.GradedAt
            FROM Submissions s
            JOIN users u ON u.id = s.StudentId
            WHERE s.AssignmentId = @assignmentId
            ORDER BY s.SubmittedAt DESC
            """;

        return Ok(await _db.QueryAsync<SubmissionView>(sql, new { assignmentId }));
    }

    [HttpGet("submissions/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var submission = await FindView(id);
        if (submission == null)
        {
            return NotFound();
        }

        if (!User.IsTeacher() && submission.StudentId != User.GetUserId())
        {
            return Forbid();
        }

        return Ok(submission);
    }

    [HttpGet("submissions/{id:int}/file")]
    public async Task<IActionResult> Download(int id)
    {
        var row = await _db.QueryFirstOrDefaultAsync<SubmissionRow>(
            "SELECT Id, StudentId, OriginalFileName, StoredFileName, Grade FROM Submissions WHERE Id = @id",
            new { id });

        if (row == null)
        {
            return NotFound();
        }

        if (!User.IsTeacher() && row.StudentId != User.GetUserId())
        {
            return Forbid();
        }

        var path = FileStorage.GetPath(row.StoredFileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { message = "Файл не найден на диске" });
        }

        return PhysicalFile(path, "application/octet-stream", row.OriginalFileName);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("submissions/{id:int}/grade")]
    public async Task<IActionResult> Grade(int id, [FromBody] GradeRequest model)
    {
        var submission = await _db.QueryFirstOrDefaultAsync<dynamic>("""
            SELECT s.Id, a.MaxGrade
            FROM Submissions s
            JOIN Assignments a ON a.Id = s.AssignmentId
            WHERE s.Id = @id
            """, new { id });

        if (submission == null)
        {
            return NotFound();
        }

        int maxGrade = (int)submission.MaxGrade;
        if (model.Grade > maxGrade)
        {
            return BadRequest(new { message = $"Оценка не может быть больше {maxGrade}" });
        }

        await _db.ExecuteAsync("""
            UPDATE Submissions
            SET Grade = @Grade, Feedback = @Feedback, GradedAt = GETUTCDATE(), GradedBy = @TeacherId
            WHERE Id = @id
            """, new
        {
            id,
            model.Grade,
            model.Feedback,
            TeacherId = User.GetUserId()
        });

        return Ok(await FindView(id));
    }

    private Task<AssignmentView?> FindAssignment(int id) =>
        _db.QueryFirstOrDefaultAsync<AssignmentView>("""
            SELECT Id, CourseId, Title, Description, DueDate, MaxGrade
            FROM Assignments WHERE Id = @id
            """, new { id });

    private Task<SubmissionView?> FindView(int id) =>
        _db.QueryFirstOrDefaultAsync<SubmissionView>("""
            SELECT s.Id, s.AssignmentId, s.StudentId, u.username AS StudentName,
                   s.OriginalFileName, s.SubmittedAt, s.Grade, s.Feedback, s.GradedAt
            FROM Submissions s
            JOIN users u ON u.id = s.StudentId
            WHERE s.Id = @id
            """, new { id });

    private async Task<bool> IsEnrolled(int courseId, int studentId) =>
        await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Enrollments WHERE CourseId = @courseId AND StudentId = @studentId",
            new { courseId, studentId }) > 0;

    private sealed class SubmissionRow
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int? Grade { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
    }
}
