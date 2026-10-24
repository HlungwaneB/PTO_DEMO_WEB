namespace PTO_Management_System
{
    /// <summary>
    /// Central place for values used all over the system (same idea as WebConstants in the CUT project).
    /// </summary>
    public class WebConstants
    {
        // Folder inside wwwroot for public images (logo, hero photo)
        public static string ImagePath = @"\images\";

        // ---- Roles (see SRS 2.1 and 6 - FR16 Role-Based Access Control) ----
        public const string RoleAdmin = "Admin";      // System Administrator
        public const string RoleOfficer = "Officer";  // PTO Officer (assists beneficiaries)
        public const string RoleUser = "User";        // Beneficiary / applicant

        // ---- Business rules (SRS FR8, FR9, 4.x) ----
        public const int PtoValidityMonths = 6;       // a PTO is valid for six months
        public const int ExpiryWarningDays = 30;      // "Expiring" starts 30 days before the expiry date
        public const decimal MaxMonthlyIncome = 3500m; // qualification: income less than R3 500 per month

        // ---- Automatic allocation: points that decide who is the most needy / most qualifying ----
        public const int MaxIncomePoints = 50;        // R0 income = 50 points, R3 500 income = 0 points (lower income = higher need)
        public const int VeteranPoints = 25;          // military veterans get a clear advantage
        public const int ElderlyPoints = 25;
        public const int PointsPerDependant = 4;      // each dependant counts ...
        public const int MaxDependantsCounted = 5;    // ... up to 5 dependants (20 points)

        // ---- Documents ----
        public const long MaxUploadBytes = 5 * 1024 * 1024; // 5 MB
        public const string DocCertifiedId = "Certified ID copy";
        public const string DocProofOfIncome = "Proof of income";
        public const string DocBirthCertificates = "Children's birth certificate(s)";

        // Session key that holds the wizard answers before the account exists
        public const string WizardSessionKey = "PTO_WIZARD";
    }
}
