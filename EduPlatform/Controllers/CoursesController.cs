using Dapper;
using EduPlatform.Helpers;
using EduPlatform.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;

namespace EduPlatform.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CoursesController : ControllerBase
{
    private readonly IDbConnection _db;

    public CoursesController(IDbConnection db)
    {
        _db = db;
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost("Add-course")]
    public async Task<IActionResult> AddCourse([FromForm] CourseDTO model, List<IFormFile>? files)
    {
        files ??= [];

        if (files.Count > 0 && files.Count != model.Lessons)
        {
            return BadRequest($"Количество загруженных файлов ({files.Count}) должно строго соответствовать количеству уроков ({model.Lessons}).");
        }

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return BadRequest("Один из выбранных файлов пуст");
            }

            await FileStorage.SaveAsync(file);
        }

        const string sqlQuery = """
            INSERT INTO Courses (NameOfCourse, Lessons, SignedUp, Description)
            VALUES (@c_name, @c_lessons, 0, @c_description);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;

        var id = await _db.ExecuteScalarAsync<int>(sqlQuery, new
        {
            c_name = model.NameOfCourse,
            c_lessons = model.Lessons,
            c_description = model.Description
        });

        return Ok(new { id, message = "Курс успешно добавлен", savedFilesCount = files.Count });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 10;

        var filter = string.IsNullOrWhiteSpace(search)
            ? ""
            : "WHERE NameOfCourse LIKE @Search";

        var countSql = $"SELECT COUNT(1) FROM Courses {filter}";
        var total = await _db.ExecuteScalarAsync<int>(countSql, new { Search = $"%{search}%" });

        var sql = $"""
            SELECT Coure_ID AS Id, NameOfCourse, Lessons, SignedUp, Description
            FROM Courses
            {filter}
            ORDER BY Coure_ID DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        var items = await _db.QueryAsync<CourseView>(sql, new
        {
            Search = $"%{search}%",
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        });

        return Ok(new PagedResult<CourseView>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    [HttpGet("AllCourses")]
    public async Task<IActionResult> AllCourses()
    {
        const string sqlQuery = """
            SELECT Coure_ID AS Id, NameOfCourse, Lessons, SignedUp, Description
            FROM Courses
            """;

        var courses = await _db.QueryAsync<CourseView>(sqlQuery);
        return Ok(courses);
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCourse(int id)
    {
        var course = await FindCourse(id);
        return course == null ? NotFound() : Ok(course);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseRequest model)
    {
        if (await FindCourse(id) == null)
        {
            return NotFound();
        }

        const string sql = """
            UPDATE Courses
            SET NameOfCourse = @NameOfCourse, Lessons = @Lessons, Description = @Description
            WHERE Coure_ID = @Id
            """;

        await _db.ExecuteAsync(sql, new { Id = id, model.NameOfCourse, model.Lessons, model.Description });
        return Ok(await FindCourse(id));
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var rows = await _db.ExecuteAsync("DELETE FROM Courses WHERE Coure_ID = @Id", new { Id = id });
        return rows == 0 ? NotFound() : NoContent();
    }

    [Authorize(Roles = "Student")]
    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id)
    {
        var course = await FindCourse(id);
        if (course == null)
        {
            return NotFound(new { message = "Курс не найден" });
        }

        var studentId = User.GetUserId();
        const string existsSql = "SELECT COUNT(1) FROM Enrollments WHERE CourseId = @CourseId AND StudentId = @StudentId";
        var already = await _db.ExecuteScalarAsync<int>(existsSql, new { CourseId = id, StudentId = studentId });
        if (already > 0)
        {
            return Conflict(new { message = "Вы уже записаны на этот курс" });
        }

        if (_db.State != ConnectionState.Open)
        {
            _db.Open();
        }

        using var tx = _db.BeginTransaction();
        await _db.ExecuteAsync(
            "INSERT INTO Enrollments (CourseId, StudentId) VALUES (@CourseId, @StudentId)",
            new { CourseId = id, StudentId = studentId },
            tx);
        await _db.ExecuteAsync(
            "UPDATE Courses SET SignedUp = SignedUp + 1 WHERE Coure_ID = @Id",
            new { Id = id },
            tx);
        tx.Commit();

        return Ok(new { message = "Вы записаны на курс", courseId = id });
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> MyCourses()
    {
        var userId = User.GetUserId();

        if (User.IsTeacher())
        {
            return Ok(await _db.QueryAsync<CourseView>("""
                SELECT Coure_ID AS Id, NameOfCourse, Lessons, SignedUp, Description
                FROM Courses
                """));
        }

        const string sql = """
            SELECT c.Coure_ID AS Id, c.NameOfCourse, c.Lessons, c.SignedUp, c.Description
            FROM Courses c
            JOIN Enrollments e ON e.CourseId = c.Coure_ID
            WHERE e.StudentId = @UserId
            """;

        return Ok(await _db.QueryAsync<CourseView>(sql, new { UserId = userId }));
    }

    [Authorize(Roles = "Teacher")]
    [HttpGet("{id:int}/students")]
    public async Task<IActionResult> CourseStudents(int id)
    {
        if (await FindCourse(id) == null)
        {
            return NotFound();
        }

        const string sql = """
            SELECT e.Id, e.CourseId, c.NameOfCourse AS CourseName, e.StudentId, u.username AS StudentName, e.EnrolledAt
            FROM Enrollments e
            JOIN Courses c ON c.Coure_ID = e.CourseId
            JOIN users u ON u.id = e.StudentId
            WHERE e.CourseId = @Id
            ORDER BY e.EnrolledAt DESC
            """;

        return Ok(await _db.QueryAsync<EnrollmentView>(sql, new { Id = id }));
    }

    [Authorize]
    [HttpGet("{id:int}/results")]
    public async Task<IActionResult> Results(int id)
    {
        if (await FindCourse(id) == null)
        {
            return NotFound();
        }

        var userId = User.GetUserId();
        if (!User.IsTeacher() && !await IsEnrolled(id, userId))
        {
            return Forbid();
        }

        var sql = """
            SELECT s.Id, s.AssignmentId, s.StudentId, u.username AS StudentName,
                   s.OriginalFileName, s.SubmittedAt, s.Grade, s.Feedback, s.GradedAt
            FROM Submissions s
            JOIN Assignments a ON a.Id = s.AssignmentId
            JOIN users u ON u.id = s.StudentId
            WHERE a.CourseId = @CourseId
            """;

        object param = new { CourseId = id };
        if (!User.IsTeacher())
        {
            sql += " AND s.StudentId = @StudentId";
            param = new { CourseId = id, StudentId = userId };
        }

        sql += " ORDER BY s.SubmittedAt DESC";
        return Ok(await _db.QueryAsync<SubmissionView>(sql, param));
    }

    private Task<CourseView?> FindCourse(int id) =>
        _db.QueryFirstOrDefaultAsync<CourseView>("""
            SELECT Coure_ID AS Id, NameOfCourse, Lessons, SignedUp, Description
            FROM Courses
            WHERE Coure_ID = @Id
            """, new { Id = id });

    private async Task<bool> IsEnrolled(int courseId, int studentId) =>
        await _db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Enrollments WHERE CourseId = @courseId AND StudentId = @studentId",
            new { courseId, studentId }) > 0;
}
