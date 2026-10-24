using System.Security.Claims;

namespace EduPlatform.Helpers;

public static class UserContext
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }

    public static bool IsTeacher(this ClaimsPrincipal user) =>
        user.IsInRole("Teacher");
}
