using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    /// <summary>
    /// The rules the SYSTEM uses to decide who gets a site. Municipal staff do not approve applications.
    ///   Income      : 0 to 50 points - the lower the monthly income, the more points (needier first)
    ///   Veteran     : +25 points     - a military veteran has a higher chance of getting a site
    ///   Elderly     : +25 points
    ///   Dependants  : +4 points each, counted up to 5 dependants (max 20)
    /// When two applicants have the same score the one who submitted first wins.
    /// </summary>
    public static class AllocationRules
    {
        public static int IncomePoints(decimal monthlyIncome)
        {
            if (monthlyIncome < 0) monthlyIncome = 0;
            if (monthlyIncome >= WebConstants.MaxMonthlyIncome) return 0;
            decimal share = (WebConstants.MaxMonthlyIncome - monthlyIncome) / WebConstants.MaxMonthlyIncome;
            return (int)Math.Round(share * WebConstants.MaxIncomePoints);
        }

        /// <summary>Returns the total score and a readable breakdown that is stored with the application.</summary>
        public static (int total, string detail) Score(Beneficiary b, int dependants)
        {
            int income = IncomePoints(b.MonthlyIncome);
            int veteran = b.IsMilitaryVeteran ? WebConstants.VeteranPoints : 0;
            int elderly = b.IsElderly ? WebConstants.ElderlyPoints : 0;
            int family = Math.Min(dependants, WebConstants.MaxDependantsCounted) * WebConstants.PointsPerDependant;
            int total = income + veteran + elderly + family;
            string detail = "Income " + income + " + Veteran " + veteran + " + Elderly " + elderly +
                            " + Dependants " + family + " = " + total;
            return (total, detail);
        }

        /// <summary>The eligibility rules from the "Reliable information" screen, checked again by the system.</summary>
        public static List<string> FailedRules(Beneficiary b)
        {
            var reasons = new List<string>();
            if (b.MonthlyIncome >= WebConstants.MaxMonthlyIncome) reasons.Add("monthly income is not below R3 500");
            if (!b.IsSouthAfrican) reasons.Add("not a South African citizen");
            if (!b.IsFirstTimeOwner) reasons.Add("not a first-time home owner");
            if (!b.IsOver21) reasons.Add("not over 21");
            if (!b.IsFirstTimeSubsidy) reasons.Add("not a first-time subsidy recipient");
            return reasons;
        }
    }
}
