using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PTO_Management_System.Models;

namespace PTO_Management_System.Data
{
    /// <summary>
    /// ONE DbContext for the whole system. It inherits from IdentityDbContext (so the login tables
    /// AspNetUsers / AspNetRoles ... live in the same Supabase PostgreSQL database as the PTO tables).
    /// In the CUT project there were two contexts (SQLiteDBContext + LoginDBContext); with one shared
    /// database it is simpler and safer to keep a single context.
    /// </summary>
    public class PtoDbContext : IdentityDbContext<IdentityUser>
    {
        public PtoDbContext(DbContextOptions<PtoDbContext> options) : base(options)
        {
        }

        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<Dependant> Dependants { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<Area> Areas { get; set; }
        public DbSet<PtoApplication> PtoApplications { get; set; }
        public DbSet<PtoStatusHistory> PtoStatusHistories { get; set; }
        public DbSet<PtoDocument> PtoDocuments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RenewalRequest> RenewalRequests { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // REQUIRED: builds the Identity tables

            // Table names in lower case (friendlier in the Supabase table editor)
            modelBuilder.Entity<Beneficiary>().ToTable("beneficiary");
            modelBuilder.Entity<Dependant>().ToTable("dependant");
            modelBuilder.Entity<Site>().ToTable("site");
            modelBuilder.Entity<Area>().ToTable("area");
            modelBuilder.Entity<PtoApplication>().ToTable("pto_application");
            modelBuilder.Entity<PtoStatusHistory>().ToTable("pto_status_history");
            modelBuilder.Entity<PtoDocument>().ToTable("document");
            modelBuilder.Entity<Notification>().ToTable("notification");
            modelBuilder.Entity<RenewalRequest>().ToTable("renewal_request");
            modelBuilder.Entity<Report>().ToTable("report");
            modelBuilder.Entity<AuditLog>().ToTable("audit_log");

            // Data integrity (SRS 7.4 Reliability): no duplicate ID numbers or site numbers
            modelBuilder.Entity<Beneficiary>().HasIndex(b => b.IdNumber).IsUnique();
            modelBuilder.Entity<Site>().HasIndex(s => s.SiteNumber).IsUnique();
            modelBuilder.Entity<Area>().HasIndex(a => a.Name).IsUnique();

            // A site can belong to only ONE live PTO (safety net against double allocation)
            modelBuilder.Entity<PtoApplication>().HasIndex(a => a.SiteId).IsUnique()
                .HasFilter("\"SiteId\" IS NOT NULL AND \"Status\" IN ('Active','Renewed','Expiring','Expired')");

            // One PTO application per beneficiary (renewals update the same PTO)
            modelBuilder.Entity<PtoApplication>().HasIndex(a => a.BeneficiaryId).IsUnique();

            modelBuilder.Entity<Beneficiary>()
                .HasMany(b => b.Dependants).WithOne(d => d.Beneficiary)
                .HasForeignKey(d => d.BeneficiaryId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Beneficiary>()
                .HasMany(b => b.Applications).WithOne(a => a.Beneficiary)
                .HasForeignKey(a => a.BeneficiaryId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Beneficiary>()
                .HasMany(b => b.Notifications).WithOne(n => n.Beneficiary)
                .HasForeignKey(n => n.BeneficiaryId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PtoApplication>()
                .HasOne(a => a.Site).WithMany().HasForeignKey(a => a.SiteId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
