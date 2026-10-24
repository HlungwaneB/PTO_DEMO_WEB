using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface IReport
    {
        IQueryable<Report> GetAll();
        Report GetById(int id);

        /// <summary>Builds the report content (CSV) from the live data, saves and returns it.</summary>
        Report Generate(string reportType, string period, string by);
    }
}
