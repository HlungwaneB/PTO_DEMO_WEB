using PTO_Management_System.Services;

namespace PTO_Management_System.Interfaces
{
    /// <summary>Talks to Supabase Auth (GoTrue) with the publishable key only. No service-role key is used.</summary>
    public interface ISupabaseAuth
    {
        Task<AuthSession> SignUpAsync(string email, string password);
        Task<AuthSession> SignInAsync(string email, string password);
        Task<AuthSession> RefreshAsync(string refreshToken);
        Task SendRecoveryCodeAsync(string email);
        Task<AuthSession> VerifyRecoveryCodeAsync(string email, string code);
        Task UpdatePasswordAsync(string accessToken, string newPassword);
        Task<string> EmailForIdNumberAsync(string idNumber);
    }
}
