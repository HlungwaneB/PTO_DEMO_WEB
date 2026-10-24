namespace PTO_Management_System.Interfaces
{
    /// <summary>FR8/FR9 - moves PTOs to Expiring / Expired and creates reminder notifications.</summary>
    public interface IExpiryService
    {
        /// <summary>Returns the number of PTOs whose status changed.</summary>
        int Refresh();
    }
}
