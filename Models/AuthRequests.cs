public class RegisterRequest
{
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginRequest
{
    public string UserName { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class ChangePasswordRequest
{
    public string UserName { get; set; } = null!;
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
