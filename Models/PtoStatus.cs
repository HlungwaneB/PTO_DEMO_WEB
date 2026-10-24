namespace PTO_Management_System.Models
{
    /// <summary>
    /// The PTO statuses from SRS FR6: Pending, Approved/Active, Expiring, Expired, Renewed, De-registered
    /// (plus Draft while the applicant is still uploading documents, and Rejected).
    /// </summary>
    public static class PtoStatus
    {
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Rejected = "Rejected";
        public const string Active = "Active";
        public const string Expiring = "Expiring";
        public const string Expired = "Expired";
        public const string Renewed = "Renewed";
        public const string Deregistered = "De-registered";

        /// <summary>Statuses that still have an expiry date to monitor (FR8).</summary>
        public static readonly string[] Monitored = { Active, Renewed, Expiring };

        /// <summary>Statuses where the site is still taken by the beneficiary.</summary>
        public static readonly string[] OccupiesSite = { Active, Renewed, Expiring, Expired };

        /// <summary>CSS class used by the coloured status badges in site.css.</summary>
        public static string BadgeClass(string status)
        {
            switch (status)
            {
                case Pending:
                case Active:
                case "Approved":
                case "Verified":
                case "Uploaded":
                case "Available":
                    return "b-green";
                case Expiring:
                    return "b-orange";
                case Expired:
                case Rejected:
                case "Missing":
                    return "b-red";
                case Renewed:
                case "Occupied":
                    return "b-blue";
                case "Check renewal":
                    return "b-amber";
                default:
                    return "b-grey"; // Draft, De-registered, Occupied ...
            }
        }

        /// <summary>How many of the 5 "formalisation journey" bars are green for a status.</summary>
        public static int PathwayStage(string status)
        {
            switch (status)
            {
                case Pending: return 1;
                case Active: return 3;
                case Expiring: return 3;
                case Expired: return 3;
                case Renewed: return 5;
                default: return 0;
            }
        }

        public static string PathwayText(string status)
        {
            switch (status)
            {
                case Pending: return "Formalisation journey · Application pending.";
                case Active: return "Formalisation journey · PTO active.";
                case Expiring: return "Formalisation journey · Renewal due soon.";
                case Expired: return "Formalisation journey · PTO expired - renewal needed.";
                case Renewed: return "Formalisation journey · PTO renewed.";
                case Rejected: return "Formalisation journey · Application not approved.";
                case Deregistered: return "Formalisation journey · PTO de-registered.";
                default: return "Formalisation journey · Not started.";
            }
        }
    }
}
