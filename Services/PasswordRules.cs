namespace PTO_Management_System.Services
{
    /// <summary>The password rules of the website (8+ characters, upper-case, lower-case and a number).</summary>
    public static class PasswordRules
    {
        /// <summary>Returns null when the password is fine, otherwise the message to show.</summary>
        public static string Check(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8) return "The password must be at least 8 characters.";
            if (!password.Any(char.IsUpper)) return "The password must contain an upper-case letter.";
            if (!password.Any(char.IsLower)) return "The password must contain a lower-case letter.";
            if (!password.Any(char.IsDigit)) return "The password must contain a number.";
            return null;
        }
    }
}
