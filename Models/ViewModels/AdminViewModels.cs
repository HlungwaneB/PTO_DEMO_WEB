using System.ComponentModel.DataAnnotations;

namespace PTO_Management_System.Models
{
    /// <summary>Screen 30 - Dashboard (FR14).</summary>
    public class DashboardViewModel
    {
        public int Beneficiaries { get; set; }
        public int Sites { get; set; }
        public int Active { get; set; }
        public int Expiring { get; set; }
        public int Expired { get; set; }
        public int Renewed { get; set; }
        public int Deregistered { get; set; }
        public int Pending { get; set; }
        public int RenewalRequests { get; set; }   // renewals done by beneficiaries (decided by the system)
        public List<AreaRow> Areas { get; set; } = new List<AreaRow>();
        public List<PtoApplication> NeedsAttention { get; set; } = new List<PtoApplication>();
    }

    /// <summary>Screen 35 - PTO detail (read only: the system makes the decisions).</summary>
    public class DetailViewModel
    {
        public PtoApplication Application { get; set; }
        public AreaRow Area { get; set; }
        public int PathwayStage { get; set; }
        public string PathwayText { get; set; }
    }

    /// <summary>List screens with filter chips + search + paging (Applications, PTO records, Sites).</summary>
    public class ListFilter
    {
        public string Filter { get; set; } = "All";
        public string Search { get; set; }
    }

    public class GenerateReportViewModel
    {
        [Required(ErrorMessage = "Choose a report")]
        public string ReportType { get; set; }

        [Required(ErrorMessage = "Enter the period, e.g. Oct 2026")]
        [StringLength(30)]
        public string Period { get; set; }
    }
}
