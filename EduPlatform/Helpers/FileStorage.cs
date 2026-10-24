namespace EduPlatform.Helpers;

public static class FileStorage
{
    public static string Root => Path.Combine(AppContext.BaseDirectory, "Files");

    public static async Task<string> SaveAsync(IFormFile file)
    {
        Directory.CreateDirectory(Root);

        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var path = Path.Combine(Root, storedName);

        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        return storedName;
    }

    public static string GetPath(string storedName) => Path.Combine(Root, storedName);
}
