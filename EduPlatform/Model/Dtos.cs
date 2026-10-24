using System.ComponentModel.DataAnnotations;

namespace EduPlatform.Model;

public class LoginRequest
{
    [Required]
    public string Login { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 4)]
    public string Password { get; set; } = string.Empty;
}

public class UpdateCourseRequest
{
    [Required(ErrorMessage = "Название курса обязательно!")]
    [StringLength(100, ErrorMessage = "Название не может быть длиннее 100 символов")]
    public string NameOfCourse { get; set; } = string.Empty;

    [Required, Range(1, 500)]
    public int Lessons { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}

public class AssignmentRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(1, 1000)]
    public int MaxGrade { get; set; } = 100;
}

public class GradeRequest
{
    [Required, Range(0, 1000)]
    public int Grade { get; set; }

    [StringLength(1000)]
    public string? Feedback { get; set; }
}

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class CourseView
{
    public int Id { get; set; }
    public string NameOfCourse { get; set; } = string.Empty;
    public int Lessons { get; set; }
    public int SignedUp { get; set; }
    public string? Description { get; set; }
}

public class AssignmentView
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int MaxGrade { get; set; }
}

public class SubmissionView
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int StudentId { get; set; }
    public string? StudentName { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public int? Grade { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAt { get; set; }
}

public class EnrollmentView
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}
