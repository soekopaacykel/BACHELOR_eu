namespace CVAPI.Models
{
public class LoginModel
{
    public string Email { get; set; }
    public string Password { get; set; }

}

}

public class OneTimeLink
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
    }