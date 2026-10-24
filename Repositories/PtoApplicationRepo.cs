using Microsoft.EntityFrameworkCore;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    /// <summary>
    /// All PTO business rules live here. The SYSTEM decides (staff do not approve): on submit it verifies the documents, scores the applicant and, when the area has reached its application target, allocates the limited sites to the most needy / most qualifying. The beneficiary renews and de-registers.
    /// Every action writes a status history row (FR15), an audit log row and, where it matters, a
    /// notification for the beneficiary (FR9) - all saved in ONE SaveChanges so they succeed together.
    /// </summary>
    public class PtoApplicationRepo : IPtoApplication
    {
        private const string SystemUser = "System";

        private readonly PtoDbContext _context;

        public PtoApplicationRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<PtoApplication> GetAll()
        {
            return _context.PtoApplications.Include(a => a.Beneficiary).Include(a => a.Site);
        }

        public PtoApplication GetById(int id)
        {
            return _context.PtoApplications
                .Include(a => a.Beneficiary).ThenInclude(b => b.Dependants)
                .Include(a => a.Site)
                .Include(a => a.Documents)
                .Include(a => a.History)
                .Include(a => a.Renewals)
                .AsSplitQuery()
                .FirstOrDefault(a => a.Id == id);
        }

        public PtoApplication ByBeneficiaryId(int beneficiaryId)
        {
            return _context.PtoApplications
                .Include(a => a.Site)
                .Include(a => a.Documents)
                .Include(a => a.History)
                .Include(a => a.Renewals)
                .AsSplitQuery()
                .FirstOrDefault(a => a.BeneficiaryId == beneficiaryId);
        }

        public PtoApplication Create(PtoApplication application)
        {
            application.CreatedAt = DateTime.UtcNow;
            application.Status = PtoStatus.Draft;
            _context.PtoApplications.Add(application);
            _context.SaveChanges();
            return application;
        }

        // ------------------------------------------------------------------ workflow

        // ================================================================== SUBMIT: the system decides

        public void Submit(int id, string by)
        {
            // One transaction: two people submitting at the same moment must not receive the same site.
            using var transaction = _context.Database.BeginTransaction();
            SubmitInternal(id, by);
            transaction.Commit();
        }

        private void SubmitInternal(int id, string by)
        {
            var app = Load(id);
            if (app.Status != PtoStatus.Draft)
                throw new InvalidOperationException("This application has already been submitted.");

            var beneficiary = _context.Beneficiaries.Include(b => b.Dependants).First(b => b.Id == app.BeneficiaryId);

            // 1. The system verifies the documents (each upload was already checked for size, type and file signature)
            var required = new List<string> { WebConstants.DocCertifiedId, WebConstants.DocProofOfIncome };
            if (beneficiary.Dependants.Count > 0) required.Add(WebConstants.DocBirthCertificates);
            var documents = _context.PtoDocuments.Where(d => d.PtoApplicationId == id).ToList();
            var missing = required.Where(r => !documents.Any(d => d.DocumentType == r && d.VerificationStatus == "Verified")).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("These documents are missing or not verified: " + string.Join(", ", missing) + ".");

            // 2. Submitted. Score the applicant (need + veteran + elderly + dependants)
            var score = AllocationRules.Score(beneficiary, beneficiary.Dependants.Count);
            app.Score = score.total;
            app.ScoreDetail = score.detail;
            app.SubmittedAt = DateTime.UtcNow;
            ChangeStatus(app, PtoStatus.Pending, by, "Application submitted, documents verified by the system. " + score.detail);
            Notify(app.BeneficiaryId, "Application received",
                "Your documents were verified. Your reference number is " + app.ReferenceNumber + ".", "success");
            Audit(by, "Submitted application", app.ReferenceNumber);

            // 3. Automatic rejection: a rule is not met
            var failed = AllocationRules.FailedRules(beneficiary);
            if (failed.Count > 0)
            {
                RejectApplication(app, "You do not meet the requirements: " + string.Join("; ", failed) + ".");
                _context.SaveChanges();
                return;
            }

            // 4. Automatic rejection: the area has no free site left (sites are limited)
            var area = _context.Areas.FirstOrDefault(a => a.Name == app.Settlement);
            if (area == null || FreeSites(app.Settlement).Count == 0)
            {
                RejectApplication(app, "There are no free sites left in " + (app.Settlement ?? "the chosen area") + ". You may apply for another area.");
                _context.SaveChanges();
                return;
            }

            // 5. Pending. Save first so the new application is counted, then see if the area reached its target
            _context.SaveChanges();
            RunAllocation(area);
        }

        /// <summary>Called when the administrator lowers an area's target: decide at once if the area is now ready.</summary>
        public void RunAllocationFor(string settlement)
        {
            var area = _context.Areas.FirstOrDefault(a => a.Name == settlement);
            if (area == null) return;
            using var transaction = _context.Database.BeginTransaction();
            RunAllocation(area);
            transaction.Commit();
        }

        /// <summary>
        /// When the number of Pending applications in the area reaches the application target, rank them by score
        /// (highest first, earliest submission wins ties). The best ones get the free sites (Active), the rest are Rejected.
        /// </summary>
        private void RunAllocation(Area area)
        {
            // Lock the area row (PostgreSQL) so only one allocation round per area runs at a time
            _context.Database.ExecuteSqlInterpolated($"select 1 from area where \"Id\" = {area.Id} for update");

            var pending = _context.PtoApplications
                .Where(a => a.Status == PtoStatus.Pending && a.Settlement == area.Name).ToList();
            if (pending.Count < area.ApplicationTarget) return; // still Pending: waiting for more applications

            var free = FreeSites(area.Name);
            var ranked = pending.OrderByDescending(a => a.Score).ThenBy(a => a.SubmittedAt).ThenBy(a => a.Id).ToList();

            for (int i = 0; i < ranked.Count; i++)
            {
                if (i < free.Count)
                    ApproveApplication(ranked[i], free[i], i + 1, ranked.Count);
                else
                    RejectApplication(ranked[i], "Not selected: you ranked " + (i + 1) + " of " + ranked.Count +
                        " in " + area.Name + " and only " + free.Count + " site(s) were available.");
            }
            _context.SaveChanges();
        }

        private List<Site> FreeSites(string settlement)
        {
            var live = PtoStatus.OccupiesSite;
            var taken = _context.PtoApplications
                .Where(a => a.SiteId != null && live.Contains(a.Status)).Select(a => a.SiteId.Value).ToList();
            return _context.Sites
                .Where(s => s.IsActive && s.Settlement == settlement && !taken.Contains(s.Id))
                .OrderBy(s => s.SiteNumber).ToList();
        }

        private void ApproveApplication(PtoApplication app, Site site, int rank, int total)
        {
            var today = DateTime.UtcNow.Date;
            app.SiteId = site.Id;
            app.ApprovedAt = DateTime.UtcNow;
            app.IssueDate = today;
            app.ExpiryDate = today.AddMonths(WebConstants.PtoValidityMonths);
            app.PtoNumber = "PTO-" + today.Year + "-" + app.Id.ToString("D5");

            ChangeStatus(app, PtoStatus.Active, SystemUser, "Approved automatically: ranked " + rank + " of " + total + ", site " + site.SiteNumber);
            Notify(app.BeneficiaryId, "Application approved",
                "You were selected (ranked " + rank + " of " + total + "). PTO " + app.PtoNumber + " for site " + site.SiteNumber +
                " is active until " + app.ExpiryDate.Value.ToString("dd MMM yyyy") + ".", "success");
            Audit(SystemUser, "Approved application", app.PtoNumber);
        }

        private void RejectApplication(PtoApplication app, string reason)
        {
            app.RejectReason = reason;
            ChangeStatus(app, PtoStatus.Rejected, SystemUser, reason);
            Notify(app.BeneficiaryId, "Application not approved", reason, "warning");
            Audit(SystemUser, "Rejected application", app.ReferenceNumber);
        }

        // ================================================================== RENEW: the beneficiary renews, the system decides

        public RenewalRequest Renew(int id, bool stillLiving, bool householdChanged, decimal? newIncome, string notes)
        {
            var app = Load(id);
            if (app.Status != PtoStatus.Active && app.Status != PtoStatus.Expiring &&
                app.Status != PtoStatus.Expired && app.Status != PtoStatus.Renewed)
                throw new InvalidOperationException("A renewal is only possible for an Active, Expiring, Expired or Renewed PTO.");

            var by = "Beneficiary " + app.ReferenceNumber;
            var request = new RenewalRequest
            {
                PtoApplicationId = id,
                StillLivingOnSite = stillLiving,
                HouseholdChanged = householdChanged,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                RequestedAt = DateTime.UtcNow,
                Status = "Approved"
            };

            string declined = null;
            if (!stillLiving)
                declined = "You must still be living at the site to renew. De-register the PTO if you moved.";
            else if (householdChanged)
            {
                if (!newIncome.HasValue)
                    declined = "Enter your new monthly income.";
                else if (newIncome.Value >= WebConstants.MaxMonthlyIncome)
                    declined = "Your new monthly income is not below R3 500, so the PTO cannot be renewed.";
                else
                {
                    var beneficiary = _context.Beneficiaries.First(b => b.Id == app.BeneficiaryId);
                    beneficiary.MonthlyIncome = newIncome.Value; // keep the record up to date
                }
            }

            _context.RenewalRequests.Add(request);

            if (declined != null)
            {
                request.Status = "Declined";
                request.Notes = declined;
                Notify(app.BeneficiaryId, "Renewal declined", declined, "warning");
                Audit(by, "Declined renewal", app.PtoNumber ?? app.ReferenceNumber);
                _context.SaveChanges();
                return request;
            }

            var today = DateTime.UtcNow.Date;
            app.IssueDate = today;
            app.ExpiryDate = today.AddMonths(WebConstants.PtoValidityMonths);
            ChangeStatus(app, PtoStatus.Renewed, by, "Renewed until " + app.ExpiryDate.Value.ToString("dd MMM yyyy"));
            Notify(app.BeneficiaryId, "PTO renewed",
                "Your PTO was renewed and is valid until " + app.ExpiryDate.Value.ToString("dd MMM yyyy") + ".", "success");
            Audit(by, "Renewed PTO", app.PtoNumber ?? app.ReferenceNumber);
            _context.SaveChanges();
            return request;
        }

        // ================================================================== DE-REGISTER: the beneficiary releases the site

        public void Deregister(int id, string reason, string by)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Record the reason for de-registering the PTO (FR11).");

            var app = Load(id);
            if (app.Status == PtoStatus.Deregistered || app.Status == PtoStatus.Draft || app.Status == PtoStatus.Rejected)
                throw new InvalidOperationException("This PTO cannot be de-registered.");

            app.DeregisteredAt = DateTime.UtcNow;
            app.DeregisterReason = reason.Trim();

            ChangeStatus(app, PtoStatus.Deregistered, by, reason.Trim());
            Notify(app.BeneficiaryId, "PTO de-registered",
                "Your PTO was de-registered. Reason: " + reason.Trim(), "warning");
            Audit(by, "De-registered PTO", app.PtoNumber ?? app.ReferenceNumber);
            _context.SaveChanges();
        }

        // ================================================================== RE-APPLY after a rejection

        public void Reapply(int id, string settlement)
        {
            var app = Load(id);
            if (app.Status != PtoStatus.Rejected && app.Status != PtoStatus.Deregistered)
                throw new InvalidOperationException("Only a rejected or de-registered application can be submitted again.");
            if (!_context.Areas.Any(a => a.Name == settlement) || FreeSites(settlement).Count == 0)
                throw new InvalidOperationException("There are no free sites in " + settlement + ".");

            var by = "Beneficiary " + app.ReferenceNumber;
            app.Settlement = settlement;
            app.SiteId = null;
            app.PtoNumber = null;
            app.ApprovedAt = null;
            app.IssueDate = null;
            app.ExpiryDate = null;
            app.DeregisteredAt = null;
            app.DeregisterReason = null;
            app.RejectReason = null;
            app.Score = 0;
            app.ScoreDetail = null;
            app.SubmittedAt = null;
            ChangeStatus(app, PtoStatus.Draft, by, "Applying again for " + settlement);
            Audit(by, "Re-applied for a PTO", app.ReferenceNumber);
            _context.SaveChanges();
        }

        public bool SiteIsOccupied(int siteId)
        {
            var live = PtoStatus.OccupiesSite;
            return _context.PtoApplications.Any(a => a.SiteId == siteId && live.Contains(a.Status));
        }

        // ------------------------------------------------------------------ helpers

        private PtoApplication Load(int id)
        {
            var app = _context.PtoApplications.FirstOrDefault(a => a.Id == id);
            if (app == null) throw new InvalidOperationException("Application not found.");
            return app;
        }

        /// <summary>Records the change in PTO_STATUS_HISTORY (FR15) and updates the status.</summary>
        private void ChangeStatus(PtoApplication app, string toStatus, string by, string note)
        {
            _context.PtoStatusHistories.Add(new PtoStatusHistory
            {
                PtoApplicationId = app.Id,
                FromStatus = app.Status,
                ToStatus = toStatus,
                ChangedBy = by,
                ChangedAt = DateTime.UtcNow,
                Note = note
            });
            app.Status = toStatus;
        }

        private void Notify(int beneficiaryId, string title, string message, string kind)
        {
            _context.Notifications.Add(new Notification
            {
                BeneficiaryId = beneficiaryId,
                Title = title,
                Message = message,
                Kind = kind,
                DateSent = DateTime.UtcNow,
                IsRead = false
            });
        }

        // The audit log reads like the wireframe: User = Administrator / System / Beneficiary, Record = PTO number, site number or name.
        private void Audit(string by, string action, string recordRef)
        {
            string user = by != null && by.StartsWith("System") ? "System"
                        : by != null && by.Contains("@") ? "Administrator"
                        : "Beneficiary";
            var owner = recordRef == null ? null : _context.Beneficiaries.FirstOrDefault(b => b.IdNumber == recordRef);
            _context.AuditLogs.Add(new AuditLog
            {
                When = DateTime.UtcNow,
                UserName = user,
                Action = action,
                RecordRef = owner != null ? owner.FullName : recordRef
            });
        }
    }
}
