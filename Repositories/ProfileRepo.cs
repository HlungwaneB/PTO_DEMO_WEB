using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Repositories
{
    public class ProfileRepo : IProfile
    {
        private readonly PtoDbContext _context;

        public ProfileRepo(PtoDbContext context)
        {
            _context = context;
        }

        public string RoleFor(string userId)
        {
            if (!Guid.TryParse(userId, out var id)) return WebConstants.RoleUser;
            var role = _context.Profiles.Where(p => p.Id == id).Select(p => p.Role).FirstOrDefault();
            return role == "admin" ? WebConstants.RoleAdmin : WebConstants.RoleUser;
        }
    }
}
