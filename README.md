# myPTO - Permission To Occupy Management System (Mangaung)

ASP.NET Core 8 MVC + Entity Framework Core + Supabase PostgreSQL + ASP.NET Core Identity.
Same structure as the CUT StudentManagementApp: Controllers / Models / Interfaces / Repositories / Views.

## Run it
1. Install the .NET 8 SDK.
2. Put the database password in user-secrets (never in Git):
   ```
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<pooler host>;Port=5432;Database=postgres;Username=postgres.fhiqwldcrpxzmonmuxet;Password=<DB PASSWORD>;SSL Mode=Require;Trust Server Certificate=true"
   ```
3. `dotnet run` - the tables, roles, administrator and sample sites are created on first start.
4. Open https://localhost:7076

Logins: `admin@gmail.com / Test!123` (administrator), `officer@gmail.com / Test!123` (officer).
Beneficiaries register through **Apply** and log in with their **ID number** + the password they create.

**Supabase:** follow `SUPABASE_SETUP.md` (connection string, first start, closing the public API, optional Storage).

The screens follow the Figma "Web (Android Expanded)" designs (Inter font, orange tabs, grey panels).

See the tutorial documents for the step-by-step build and the Git workflow for the 6 team members.
