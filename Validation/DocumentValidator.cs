namespace PTO_Management_System.Validation
{
    /// <summary>
    /// Verifies an uploaded supporting document: size, extension AND the real file signature
    /// (so a .exe renamed to .pdf is rejected).
    /// </summary>
    public static class DocumentValidator
    {
        private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

        /// <summary>Returns an error message, or null when the file is acceptable.</summary>
        public static async Task<string> ValidateAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return "Choose a file to upload.";

            if (file.Length > WebConstants.MaxUploadBytes)
                return "The file is too large. The maximum size is 5 MB.";

            var extension = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return "Only PDF, JPG or PNG files are allowed.";

            var header = new byte[8];
            using (var stream = file.OpenReadStream())
            {
                int read = await stream.ReadAsync(header, 0, header.Length);
                if (read < 4) return "The file is empty or damaged.";
            }

            bool isPdf = header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46;               // %PDF
            bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;               // .PNG
            bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;                                    // JPEG

            bool matches = (extension == ".pdf" && isPdf) || (extension == ".png" && isPng) ||
                           ((extension == ".jpg" || extension == ".jpeg") && isJpg);

            return matches ? null : "The file content does not match its type. Upload a real PDF, JPG or PNG.";
        }
    }
}
