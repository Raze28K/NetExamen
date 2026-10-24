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
public class AssignmentsController : ControllerBase
{
    private readonly IDbConnection _db;

    public AssignmentsController(IDbConnection db)
    {
        _db = db;
    }

    [HttpGet("courses/{courseId:int}/assignments")]
    public async Task<IActionResult> GetByCourse(int courseId)
    {
        if (!await CourseExists(courseId))
        {
            return NotFound();
        }

        if (!User.IsTeacher() && !await IsEnrolled(courseId, User.GetUserId()))
        {
            return Forbid();
        }

        const string sql = """
            SELECT Id, CourseId, Title, Description, DueDate, MaxGrade
            FROM Assignments
            WHERE CourseId = @courseId
            ORDER BY Id DESC
            """;

        return Ok(await _db.QueryAsync<AssignmentView>(sql, new { courseId }));
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("courses/{courseId:int}/assignments")]
    public async Task<IActionResult> Create(int courseId, [FromBody] AssignmentRequest model)
    {
        if (!await CourseExists(courseId))
        {
            return NotFound(new { message = "Курс не найден" });
        }

        const string sql = """
            INSERT INTO Assignments (CourseId, Title, Description, DueDate, MaxGrade)
            VALUES (@courseId, @Title, @Description, @DueDate, @MaxGrade);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        var id = await _db.ExecuteScalarAsync<int>(sql, new
        {
            courseId,
            model.Title,
            model.Description,
            model.DueDate,
            model.MaxGrade
        });

        return CreatedAtAction(nameof(GetById), new { id }, await Find(id));
    }

    [HttpGet("assignments/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var assignment = await Find(id);
        if (assignment == null)
        {
            return NotFound();
        }

        if (!User.IsTeacher() && !await IsEnrolled(assignment.CourseId, User.GetUserId()))
        {
            return Forbid();
        }

        return Ok(assignment);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("assignments/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AssignmentRequest model)
    {
        if (await Find(id) == null)
        {
            return NotFound();
        }

        const string sql = """
            UPDATE Assignments
            SET Title = @Title, Description = @Description, DueDate = @DueDate, MaxGrade = @MaxGrade
            WHERE Id = @id
            """;

        await _db.ExecuteAsync(sql, new { id, model.Title, model.Description, model.DueDate, model.MaxGrade });
        return Ok(await Find(id));
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("assignments/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rows = await _db.ExecuteAsync("DELETE FROM Assignments WHERE Id = @id", new { id });
        return rows == 0 ? NotFound() : NoContent();
    }

    private Task<AssignmentView?> Find(int id) =>
        _db.QueryFirstOrDefaultAsync<AssignmentView>("""
            SELECT Id, CourseId, Title, Description, DueDate, MaxGrade
            FROM Assignments
            WHERE Id = @id
            """, new { id });

    private async Task<bool> CourseExists(int id) =>
        await _db.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Courses WHERE Coure_ID = @id", new { id }) > 0;

    private async Task<bool> IsEnrolled(int courseId, int studentId) =>
        await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Enrollments WHERE CourseId = @courseId AND StudentId = @studentId",
            new { courseId, studentId }) > 0;
}
