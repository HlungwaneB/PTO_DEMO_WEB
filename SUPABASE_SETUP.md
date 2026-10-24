# Supabase setup for myPTO

Step-by-step: from the existing Supabase project to a running site. (Also Section 3 of the tutorial.)

## 3.0 What Supabase does here (read this first)

Supabase gives you a hosted **PostgreSQL database**. The myPTO web application connects to it directly with Entity Framework Core. Everything is stored there: the ASP.NET Identity tables (users, password hashes, roles) and the PTO tables (beneficiary, dependant, site, area, pto_application, pto_status_history, document, notification, renewal_request, report, audit_log).

* You do **not** create any tables by hand. On the first start the application creates them (`EnsureCreated`) and seeds the roles, the administrator, the officer and eight sample sites.
* Users are **not** stored in Supabase "Authentication". Log-in is done by ASP.NET Core Identity, so the users live in the table `AspNetUsers`.
* The only secret is the **database password**. It goes into *user-secrets* on your own computer and is never committed to GitHub.
* Your project already exists: reference `fhiqwldcrpxzmonmuxet`, URL `https://fhiqwldcrpxzmonmuxet.supabase.co`. These two values are already in `appsettings.json`.

## 3.1 Get the connection details (Benny, once)

1. Open <https://supabase.com/dashboard> and sign in. Click the project **fhiqwldcrpxzmonmuxet**.
2. Click the **Connect** button at the top of the project page. Open the **Connection string** tab (Supabase may also call it *Direct* / *ORMs*).
3. Set **Method** to **Session pooler**. Do **not** use "Direct connection": the direct host only accepts IPv6 and fails on most home and campus networks.
4. Write down two values from the string Supabase shows:
   * the **host**, for example `aws-0-eu-west-1.pooler.supabase.com` (the part after `aws-0-` is your region and can differ: copy it exactly as shown, it may also start with `aws-1-`),
   * the **user**, which must look like `postgres.fhiqwldcrpxzmonmuxet` (the word *postgres*, a dot, then the project reference).
5. The port of the Session pooler is **5432**.

## 3.2 Set or reset the database password (Benny, once)

1. In the project open **Project Settings** (gear icon, bottom left) > **Database**.
2. Under **Database password** click **Reset database password**. Choose a password of 16 or more letters and digits. **Do not use** these characters: `@ : / ? # ; = " '` (they break the connection string).
3. Click **Reset password** and copy the new password to a private place (a password manager). Supabase does not show it again.
4. Send the password to the other five members privately (never in a GitHub issue, a pull request or a group chat that is public).

## 3.3 Store the connection string on your computer (every member, once)

Open a terminal **inside the folder that contains `PTO_Management_System.csproj`** and run one command (all on one line). Replace `YOUR_REGION_HOST` and `YOUR_DB_PASSWORD`:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=YOUR_REGION_HOST;Port=5432;Database=postgres;Username=postgres.fhiqwldcrpxzmonmuxet;Password=YOUR_DB_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets list
```

`dotnet user-secrets list` must print the connection string. The project already has a `UserSecretsId`, so no `init` is needed.

**Visual Studio alternative:** right-click the project > **Manage User Secrets**. A file `secrets.json` opens. Replace its content with:

```
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=YOUR_REGION_HOST;Port=5432;Database=postgres;Username=postgres.fhiqwldcrpxzmonmuxet;Password=YOUR_DB_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

That file lives outside the project folder, so Git never sees it.

All six of you share the same Supabase database, so test data that one person enters is visible to everyone. Use obviously fake test data.

## 3.4 First start: the app creates the tables

```
dotnet run
```

(or press **F5** in Visual Studio) and open <https://localhost:7076>. On the first start the application creates all tables, the roles (Admin, Officer, User), the administrator `admin@gmail.com` / `Test!123`, the officer `officer@gmail.com` / `Test!123` and eight sample sites (BS-0142 ... MZ-0102). If the connection fails the site still starts and the console shows the reason (see 3.8).

## 3.5 Check that it worked

1. In Supabase open **Table Editor**. In the schema **public** you must see: `area`, `audit_log`, `beneficiary`, `dependant`, `document`, `notification`, `pto_application`, `pto_status_history`, `renewal_request`, `report`, `site` and the Identity tables `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` and four more.
2. Open the table `site`: eight rows. Open `AspNetUsers`: two rows (admin and officer). Open `area`: three rows (Botshabelo, Thaba Nchu, Mangweni Z1).
3. In the web application click **Login as admin**, log in with `admin@gmail.com` / `Test!123`: you reach the Dashboard.

## 3.6 Close the public REST API (Benny, once, after the first start)

Supabase exposes every table in the `public` schema through a REST API that anybody holding the *publishable* key can call. The web application does **not** use that API (it connects directly as the `postgres` role), so we switch **Row Level Security** on for every table and add no policies: the REST API is then closed and the application keeps working.

1. Supabase > **SQL Editor** > **New query**.
2. Open `Database/supabase_setup.sql` from the project, copy everything, paste it into the editor and press **Run**. It must finish with *Success. No rows returned*.
3. Check: **Table Editor** now shows a small "RLS enabled" label on each table. In **Advisors** (Database > Advisors, or the Security Advisor) there must be no more "RLS disabled in public" warnings.

Run the script **after** the first start, because the tables must exist first. If you ever recreate the tables (see the fresh-start steps in 3.8), run it again.

## 3.7 Optional: store the documents in Supabase Storage

By default the uploaded documents are saved on disk (`App_Data/uploads`) and only the database is in Supabase. To keep the documents in Supabase Storage as well:

1. The SQL script (3.6) already created a **private** bucket called `pto-documents`. Check it under **Storage** > **Buckets**.
2. **Project Settings** > **API Keys** > copy the **service_role** (secret) key. It bypasses all security: never put it in a file that goes to GitHub, never share it in public.
3. Store it with user-secrets:

```
dotnet user-secrets set "Storage:Provider" "Supabase"
dotnet user-secrets set "Supabase:ServiceRoleKey" "PASTE_SERVICE_ROLE_KEY"
```

4. Restart the application, upload a document in an application and check that the file appears in **Storage** > **pto-documents**.

To go back to disk storage: `dotnet user-secrets set "Storage:Provider" "Local"`.

## 3.8 Troubleshooting Supabase

| Message or problem | Cause and fix |
|---|---|
| `Database could not be initialised` in the console | The connection string is missing or wrong. Run `dotnet user-secrets list` (inside the folder with the .csproj) and compare it with 3.3. |
| `28P01: password authentication failed` | Wrong password, or a forbidden character in it. Reset the password (3.2) and run the user-secrets command again. |
| `Tenant or user not found` | The user is not `postgres.fhiqwldcrpxzmonmuxet` (with the project reference) or the host region is wrong. Copy both again from **Connect** (3.1). |
| Timeout, `Network is unreachable`, `No such host is known` | You used the *Direct connection* (IPv6) or mistyped the host. Use the **Session pooler** host and port 5432. |
| `SSL` or certificate error | Keep `SSL Mode=Require;Trust Server Certificate=true` at the end of the string. |
| `relation "..." does not exist` or `column ... does not exist` | `EnsureCreated` does not change existing tables, so a database created by an older version of the code lacks the new ones. Fresh start: **SQL Editor** > run `drop schema public cascade; create schema public; grant all on schema public to postgres, anon, authenticated, service_role;` then start the app again (it recreates and re-seeds everything) and run `supabase_setup.sql` again. This deletes all data. |
| The Table Editor shows no tables | You are looking at another schema. Choose **public** in the schema drop-down. |
| Uploading a document fails with *Supabase Storage upload failed (401/403)* | `Storage:Provider` is `Supabase` but the service-role key is missing or wrong (3.7), or the bucket `pto-documents` does not exist. |
| Everything worked yesterday, today it does not connect | Free Supabase projects **pause** after a week without activity. Open the dashboard and click **Restore project**, wait a few minutes. |
