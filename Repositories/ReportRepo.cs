using System.Text;
using Microsoft.EntityFrameworkCore;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    public class ReportRepo : IReport
    {
        public static readonly string[] ReportTypes =
        {
            "PTO status summary",
            "Expiring PTOs (30 days)",
            "Sites by settlement"
        };

        private readonly PtoDbContext _context;

        public ReportRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<Report> GetAll()
        {
            return _context.Reports.OrderByDescending(r => r.GeneratedAt);
        }

        public Report GetById(int id)
        {
            return _context.Reports.FirstOrDefault(r => r.Id == id);
        }

        public Report Generate(string reportType, string period, string by)
        {
            var csv = new StringBuilder();

            switch (reportType)
            {
                case "PTO status summary":
                    csv.AppendLine("Status,Number of PTOs");
                    foreach (var status in new[] { PtoStatus.Pending, PtoStatus.Active, PtoStatus.Expiring,
                                                   PtoStatus.Expired, PtoStatus.Renewed, PtoStatus.Deregistered, PtoStatus.Rejected })
                    {
                        int count = _context.PtoApplications.Count(a => a.Status == status);
                        csv.AppendLine(Csv(status) + "," + count);
                    }
                    break;

                case "Expiring PTOs (30 days)":
                    // ID numbers are deliberately NOT included (SRS 9.5 - protect personal information)
                    csv.AppendLine("PTO number,Beneficiary,Site,Expiry date,Days left");
                    var today = DateTime.UtcNow.Date;
                    var expiring = _context.PtoApplications
                        .Include(a => a.Beneficiary).Include(a => a.Site)
                        .Where(a => a.Status == PtoStatus.Expiring)
                        .OrderBy(a => a.ExpiryDate).ToList();
                    foreach (var a in expiring)
                    {
                        int days = a.ExpiryDate.HasValue ? (int)(a.ExpiryDate.Value.Date - today).TotalDays : 0;
                        csv.AppendLine(string.Join(",",
                            Csv(a.PtoNumber), Csv(a.Beneficiary?.FullName), Csv(a.Site?.SiteNumber),
                            Csv(a.ExpiryDate?.ToString("yyyy-MM-dd")), days));
                    }
                    break;

                case "Sites by settlement":
                    csv.AppendLine("Settlement,Total sites,Occupied,Available");
                    var live = PtoStatus.OccupiesSite;
                    var occupiedIds = _context.PtoApplications
                        .Where(a => a.SiteId != null && live.Contains(a.Status))
                        .Select(a => a.SiteId.Value).ToList();
                    var sites = _context.Sites.Where(s => s.IsActive).ToList();
                    foreach (var group in sites.GroupBy(s => s.Settlement).OrderBy(g => g.Key))
                    {
                        int total = group.Count();
                        int occupied = group.Count(s => occupiedIds.Contains(s.Id));
                        csv.AppendLine(Csv(group.Key) + "," + total + "," + occupied + "," + (total - occupied));
                    }
                    break;

                default:
                    throw new ArgumentException("Unknown report type.");
            }

            var report = new Report
            {
                Name = reportType,
                Period = period,
                GeneratedAt = DateTime.UtcNow,
                GeneratedBy = by,
                Content = csv.ToString()
            };
            _context.Reports.Add(report);
            _context.AuditLogs.Add(new AuditLog
            {
                When = DateTime.UtcNow,
                UserName = by,
                Action = "Generated report: " + reportType,
                RecordRef = period
            });
            _context.SaveChanges();
            return report;
        }

        private static string Csv(string value)
        {
            if (value == null) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
