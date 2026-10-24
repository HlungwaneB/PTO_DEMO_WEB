using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PTO_Management_System.Validation
{
    /// <summary>
    /// Verifies a South African ID number (13 digits): YYMMDD date of birth, SSSS gender sequence,
    /// C citizenship digit (0 = citizen, 1 = permanent resident), A (8 or 9), Z Luhn check digit.
    /// This is the "verification" required by the team: a made-up ID number is rejected.
    /// </summary>
    public static class SaIdValidator
    {
        public static bool TryParse(string id, out DateTime dateOfBirth, out string gender, out bool isCitizen)
        {
            dateOfBirth = default(DateTime);
            gender = null;
            isCitizen = false;

            if (string.IsNullOrWhiteSpace(id) || id.Length != 13 || !id.All(char.IsDigit))
                return false;

            int yy = int.Parse(id.Substring(0, 2));
            int mm = int.Parse(id.Substring(2, 2));
            int dd = int.Parse(id.Substring(4, 2));

            // Two-digit year: if it is later than this year's last two digits it must be 19xx
            int currentYY = DateTime.UtcNow.Year % 100;
            int year = yy > currentYY ? 1900 + yy : 2000 + yy;

            try { dateOfBirth = new DateTime(year, mm, dd); }
            catch (ArgumentOutOfRangeException) { return false; }

            if (dateOfBirth.Date > DateTime.UtcNow.Date) return false;

            int sequence = int.Parse(id.Substring(6, 4));
            gender = sequence >= 5000 ? "Male" : "Female";
            isCitizen = id[10] == '0';

            return PassesLuhn(id);
        }

        public static bool IsValid(string id)
        {
            return TryParse(id, out _, out _, out _);
        }

        /// <summary>Luhn checksum: double every second digit from the right (the check digit is not doubled).</summary>
        private static bool PassesLuhn(string id)
        {
            int sum = 0;
            bool doubleIt = false;
            for (int i = id.Length - 1; i >= 0; i--)
            {
                int d = id[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }
            return sum % 10 == 0;
        }

        public static int AgeOn(DateTime dateOfBirth, DateTime today)
        {
            int age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    /// <summary>[SouthAfricanId] - use on a string property to validate the 13-digit ID number.</summary>
    public class SouthAfricanIdAttribute : ValidationAttribute
    {
        public SouthAfricanIdAttribute()
        {
            ErrorMessage = "Enter a valid 13-digit South African ID number.";
        }

        public override bool IsValid(object value)
        {
            var text = value as string;
            if (string.IsNullOrEmpty(text)) return true; // [Required] handles empty values
            return SaIdValidator.IsValid(text);
        }
    }

    /// <summary>Normalises and validates South African phone numbers (0821234567 or +27 82 123 4567).</summary>
    public static class PhoneHelper
    {
        /// <summary>Returns +27XXXXXXXXX or null when the number is not valid.</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var cleaned = new string(input.Where(c => char.IsDigit(c) || c == '+').ToArray());
            string national;

            if (cleaned.StartsWith("+27")) national = cleaned.Substring(3);
            else if (cleaned.StartsWith("0027")) national = cleaned.Substring(4);
            else if (cleaned.StartsWith("27") && cleaned.Length == 11) national = cleaned.Substring(2);
            else if (cleaned.StartsWith("0")) national = cleaned.Substring(1);
            else return null;

            if (national.Length != 9 || !national.All(char.IsDigit) || national[0] == '0') return null;
            return "+27" + national;
        }
    }

    /// <summary>[SaCellphone] - optional field is allowed to be empty; if filled it must be a valid number.</summary>
    public class SaCellphoneAttribute : ValidationAttribute
    {
        public SaCellphoneAttribute()
        {
            ErrorMessage = "Enter a valid South African number, e.g. 082 555 0142 or +27 82 555 0142.";
        }

        public override bool IsValid(object value)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text)) return true;
            return PhoneHelper.Normalize(text) != null;
        }
    }

    /// <summary>Names may only contain letters, spaces, hyphens and apostrophes.</summary>
    public static class NameRules
    {
        public const string Pattern = @"^[A-Za-z][A-Za-z\s\-']*$";
        public const string Message = "Only letters, spaces, hyphens and apostrophes are allowed.";
    }
}
