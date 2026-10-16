# AcxiomCRM

AcxiomCRM is a role-aware ASP.NET Core MVC customer relationship management application built with C#, Entity Framework Core, SQL Server, ASP.NET Core Identity, Bootstrap, JavaScript, and Chart.js.

## What is included

- Identity authentication: registration, login, logout, password rotation, secure password hashes, failed-login lockout, and inactive-account session invalidation.
- Three protected roles: `Admin`, `Manager`, and `Sales Executive`. Admins see all data; managers see their reporting tree; sales executives see only their own records.
- Responsive dashboard with KPIs, sales-pipeline chart, opportunity mix, leads, and due follow-ups.
- Full CRUD workflows for customers, leads, opportunities, and activities/follow-ups; searchable/filterable tables; lead-to-customer conversion; relationship-aware detail timelines.
- Admin user, role, and audit-log management.
- Customer, lead, and opportunity REST endpoints under `/api/customers`, `/api/leads`, and `/api/opportunities`.
- Reports for customer status, lead status/source, sales pipeline/forecast, and follow-up activity.
- Server/client validation, antiforgery validation, duplicate-customer email prevention backed by a unique SQL index, business-rule constraints, audit logs, row-version concurrency checks, and explicit EF Core relationships/check constraints.

## Run locally

1. Install SQL Server LocalDB, SQL Server Express, or SQL Server Developer Edition.
2. Update `ConnectionStrings:DefaultConnection` in `appsettings.json` or set `ConnectionStrings__DefaultConnection` as an environment variable.
3. Restore and run:

   ```powershell
   dotnet restore
   dotnet run
   ```

EF Core creates the schema at first startup using `EnsureCreated`. This is intended for this self-contained assignment app. For production deployment, use managed migrations and a deployment-controlled SQL Server database.

In the Development environment only, the first launch seeds these roles and a bootstrap administrator:

- Email: `admin@acxiomcrm.local`
- Password: `ChangeMe!2026`

Change that password immediately. Production does not contain a seeded password. Supply `SeedAdmin__Email`, `SeedAdmin__Password`, `SeedAdmin__FirstName`, and `SeedAdmin__LastName` securely if a bootstrap production administrator is needed.

## REST API notes

All API endpoints require an authenticated Identity session and enforce the same role/ownership scope as MVC pages. State-changing requests are protected by the application-wide ASP.NET Core antiforgery filter; when calling the API from a browser, include a valid request-verification token obtained from an authenticated page/form.

Example read request after login:

```http
GET /api/customers?search=acme
```

## Roles

| Role | Access |
| --- | --- |
| Admin | All CRM records, users, roles, audit logs, and reports |
| Manager | Their own records and all direct/indirect reports' records, plus reports |
| Sales Executive | Only records assigned to them |
