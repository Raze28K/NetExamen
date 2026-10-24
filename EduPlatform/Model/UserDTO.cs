namespace EduPlatform.Model
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string username { get; set; } = string.Empty;
        public string password_hash { get; set; } = string.Empty;
        public int Role_Id { get; set; }

    }
}
