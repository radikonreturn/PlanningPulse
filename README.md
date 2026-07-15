# PlanningPulse ⚡

PlanningPulse is a modern, multi-tenant Manufacturing Resource Planning (MRP) and Scheduling system built using .NET 8, Entity Framework Core, and Blazor Server. It is designed to help teams orchestrate their bill of materials, inventory, work centers, routings, and production schedules under a robust tenant-isolated architecture.

---

## 🚀 Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
* PowerShell (to run helper scripts).

### Running the App
To start the development server, run the helper PowerShell script in the root directory:
```powershell
./run.ps1
```
Or use the `dotnet CLI` directly:
```bash
dotnet run --project src/PlanningPulse.Web --environment Development
```

Once started, the application will automatically:
1. Apply the SQLite migrations/schema dynamically (using `EnsureCreatedAsync()`).
2. Seed the default demo workspace and admin user.
3. Listen locally at **http://localhost:5000**.

---

## 🔑 Default Credentials

The registration flow has been disabled for simplicity. You can sign in using the automatically seeded demo workspace:

| Field | Value |
|---|---|
| **Email** | `admin@demo.com` |
| **Workspace ID** | `demo` |
| **Password** | `Admin123!` |

---

## 🏗️ Project Architecture

PlanningPulse follows Clean Architecture patterns:

* **PlanningPulse.Domain**: Contains core entity models (Item, Bom, WorkCenter, Routing, ProductionOrder, Tenant, ApplicationUser, etc.) and tenant/audit marker interfaces.
* **PlanningPulse.Application**: Application services, models, and interfaces (like the `MrpEngine` and authentication abstractions).
* **PlanningPulse.Infrastructure**: Data persistence layer (`PlanningPulseDbContext` mapping SQLite), custom JWT token services, middleware for extracting tenant claims, and the `DatabaseSeeder`.
* **PlanningPulse.Web**: Blazor Server UI (interactive dashboard, MRP, Scheduling, Bills of Materials, Inventory, Suppliers, and Production orders pages) and controllers for token auth.
* **PlanningPulse.Tests**: Isolation and domain logic validation tests.

---

## 🛡️ Multi-Tenancy

Data isolation is built directly into the persistence layer. 
* Entities inheriting from `ITenantOwned` have an automated EF query filter applied to ensure users can only retrieve or modify records belonging to their current `TenantId`.
* Tenant ID verification is managed dynamically at the middleware level via `TenantClaimsMiddleware` and transient context interfaces.
