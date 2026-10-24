using System.Text.Json;

namespace PTO_Management_System.Models
{
    /// <summary>
    /// Holds the answers of the PTO application wizard (Reliable information -> Personal -> Contact ->
    /// Dependants) while the applicant has no account yet. It lives in the server-side Session and is
    /// written to the database when the applicant creates the password. The password is NEVER stored here.
    /// </summary>
    public class WizardState
    {
        public EligibilityViewModel Eligibility { get; set; }
        public bool Eligible { get; set; }
        public PersonalViewModel Personal { get; set; }
        public ContactViewModel Contact { get; set; }
        public List<DependantViewModel> Dependants { get; set; } = new List<DependantViewModel>();
    }

    /// <summary>Small helpers to keep objects in the Session as JSON.</summary>
    public static class SessionJsonExtensions
    {
        public static void SetJson<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T GetJson<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            return json == null ? default(T) : JsonSerializer.Deserialize<T>(json);
        }
    }
}
