namespace PTO_Management_System.Interfaces
{
    /// <summary>Reads the role (admin or user) that is stored next to each Supabase login.</summary>
    public interface IProfile
    {
        /// <summary>Returns WebConstants.RoleAdmin or WebConstants.RoleUser.</summary>
        string RoleFor(string userId);
    }
}
