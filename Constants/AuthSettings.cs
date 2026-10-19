namespace NetMvcAuthV2.Constants;

public static class AuthSettings
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxFailedAttempts = 5;
    public const int LockoutMinutes = 10;
    public static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
}
