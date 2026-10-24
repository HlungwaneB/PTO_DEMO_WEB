namespace PTO_Management_System.Interfaces
{
    /// <summary>
    /// Where uploaded documents are kept. Two implementations exist:
    /// LocalFileStorage (App_Data folder, default) and SupabaseFileStorage (Supabase Storage bucket).
    /// </summary>
    public interface IFileStorage
    {
        Task SaveAsync(IFormFile file, string storedFileName);
        Task<Stream> OpenReadAsync(string storedFileName);
        Task DeleteAsync(string storedFileName);
    }
}
