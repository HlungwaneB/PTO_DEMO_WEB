-- =====================================================================
-- myPTO - Supabase hardening script.  Run ONCE in: Supabase > SQL Editor
-- Run it AFTER the web app has started once (the app creates the tables).
--
-- Why: Supabase exposes every table in the "public" schema through a REST API
-- that anybody holding the publishable (anon) key can call. The ASP.NET app does
-- NOT use that API for data - it connects directly as the "postgres" role, which
-- bypasses Row Level Security. So we switch RLS on for every table and add no
-- policies: the REST API is then closed, the web app keeps working.
-- =====================================================================

alter table if exists public.beneficiary          enable row level security;
alter table if exists public.dependant            enable row level security;
alter table if exists public.site                 enable row level security;
alter table if exists public.area                 enable row level security;
alter table if exists public.pto_application      enable row level security;
alter table if exists public.pto_status_history   enable row level security;
alter table if exists public.document             enable row level security;
alter table if exists public.notification         enable row level security;
alter table if exists public.renewal_request      enable row level security;
alter table if exists public.report               enable row level security;
alter table if exists public.audit_log            enable row level security;

-- ASP.NET Core Identity tables (users, password hashes, roles)
alter table if exists public."AspNetUsers"        enable row level security;
alter table if exists public."AspNetRoles"        enable row level security;
alter table if exists public."AspNetUserRoles"    enable row level security;
alter table if exists public."AspNetUserClaims"   enable row level security;
alter table if exists public."AspNetUserLogins"   enable row level security;
alter table if exists public."AspNetUserTokens"   enable row level security;
alter table if exists public."AspNetRoleClaims"   enable row level security;

-- =====================================================================
-- OPTIONAL - only if you set  Storage:Provider = "Supabase"
-- Creates a PRIVATE bucket for the uploaded documents. The app uploads with the
-- service-role key (never put that key in GitHub - use dotnet user-secrets).
-- =====================================================================
insert into storage.buckets (id, name, public)
values ('pto-documents', 'pto-documents', false)
on conflict (id) do nothing;
