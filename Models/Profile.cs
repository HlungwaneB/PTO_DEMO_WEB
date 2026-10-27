namespace PTO_Management_System.Models
{
    /// <summary>
    /// One row per Supabase login (table: profiles). The login itself (e-mail and password) is kept by
    /// Supabase Auth; this row only says whether the person is an "admin" or a normal "user" (beneficiary).
    /// </summary>
    public class Profile
    {
        public Guid Id { get; set; }          // the Supabase user id
        public string Role { get; set; }      // "admin" or "user"
        public DateTime CreatedAt { get; set; }
    }
}
