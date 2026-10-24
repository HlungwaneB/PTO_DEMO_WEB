namespace PTO_Management_System.Interfaces
{
    /// <summary>Creates the tables (EnsureCreated) and seeds roles, the administrator and sample sites.</summary>
    public interface IDBInitializer
    {
        Task InitializeAsync();
    }
}
