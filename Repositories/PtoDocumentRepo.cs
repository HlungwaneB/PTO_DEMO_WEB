using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    public class PtoDocumentRepo : IPtoDocument
    {
        private readonly PtoDbContext _context;

        public PtoDocumentRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<PtoDocument> ForApplication(int applicationId)
        {
            return _context.PtoDocuments.Where(d => d.PtoApplicationId == applicationId);
        }

        public PtoDocument GetById(int id)
        {
            return _context.PtoDocuments.FirstOrDefault(d => d.Id == id);
        }

        public PtoDocument Add(PtoDocument document)
        {
            document.UploadedAt = DateTime.UtcNow;
            _context.PtoDocuments.Add(document);
            _context.SaveChanges();
            return document;
        }

        /// <summary>Deletes the database row and returns it so the caller can delete the stored file too.</summary>
        public PtoDocument Remove(int id)
        {
            var document = _context.PtoDocuments.FirstOrDefault(d => d.Id == id);
            if (document == null) return null;
            _context.PtoDocuments.Remove(document);
            _context.SaveChanges();
            return document;
        }

        public void SetVerification(int id, string status, string by)
        {
            var document = _context.PtoDocuments.FirstOrDefault(d => d.Id == id);
            if (document == null) return;
            document.VerificationStatus = status;
            _context.AuditLogs.Add(new AuditLog
            {
                When = DateTime.UtcNow,
                UserName = by,
                Action = "Document " + status.ToLower() + ": " + document.DocumentType,
                RecordRef = "Application #" + document.PtoApplicationId
            });
            _context.SaveChanges();
        }
    }
}
