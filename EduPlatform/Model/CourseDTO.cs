using System.ComponentModel.DataAnnotations;

namespace EduPlatform.Model;

public class CourseDTO
{
    [Required(ErrorMessage = "Название курса обязательно!")]
    [StringLength(100, ErrorMessage = "Название не может быть длиннее 100 символов")]
    public string NameOfCourse { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите количество уроков в этом курсе!")]
    [Range(1, 500)]
    public int Lessons { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }
}
