using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Storage
{
    /// <summary>
    /// Default storage: files go to App_Data/uploads on the server. This folder is NOT inside wwwroot,
    /// so nobody can open a document by typing its URL - documents are only served by
    /// DocumentsController.Download after the user has been checked (SRS 9.5: ID numbers must not be disclosed).
    /// </summary>
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(IWebHostEnvironment environment)
        {
            _root = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");
            Directory.CreateDirectory(_root);
        }

        public async Task SaveAsync(IFormFile file, string storedFileName)
        {
            var path = SafePath(storedFileName);
            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }

        public Task<Stream> OpenReadAsync(string storedFileName)
        {
            var path = SafePath(storedFileName);
            if (!File.Exists(path)) return Task.FromResult<Stream>(null);
            return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read));
        }

        public Task DeleteAsync(string storedFileName)
        {
            var path = SafePath(storedFileName);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        /// <summary>Strips any folder part so a stored name can never escape the uploads folder.</summary>
        private string SafePath(string storedFileName)
        {
            return Path.Combine(_root, Path.GetFileName(storedFileName));
        }
    }
}
