<img width="995" height="651" alt="Screenshot 2026-05-08 at 10 59 00" src="https://github.com/user-attachments/assets/f4178b34-8e7b-4149-8a03-e6f8c402ea1b" />

# Rendezvous

Rendezvous is a web-based appointment booking platform for service businesses and their customers. The current product focus is barber shops, while the domain model remains generic enough to support other appointment-driven businesses later.

Customers can browse approved businesses, inspect services, review available appointment times, and submit appointment requests. Businesses manage services, staff, working hours, appointment requests, approved appointments, invitations, and profile content. Admin users manage platform-level business approval and user visibility.

## Core Capabilities

- Public business discovery and public business profile pages
- Service catalog management for business owners
- Staff management with business-scoped employee access
- Customer appointment request creation and cancellation
- Owner and employee appointment request review
- Approved appointment cancellation with role-specific access
- Business photo upload and public photo display
- Employee invitation acceptance flow
- Admin business management and user visibility
- Email confirmation and notification support

## User Roles

- **Customer**: browses businesses, requests appointments, and manages their own requests and appointments.
- **Employee**: reviews and manages appointment requests and approved appointments assigned to their staff profile.
- **Business owner**: manages business operations, services, staff, availability, appointment requests, appointments, invitations, and business profile content.
- **Admin**: reviews businesses, manages business status, and inspects platform users.

Users can participate in multiple contexts. Platform-level roles stay small, while business-specific permissions are represented by business membership records.

## Requirements

- .NET 8 SDK
- Node.js 20 or newer
- npm
- PostgreSQL 16
- Entity Framework Core CLI tools

## Tech Stack

- **Backend**: ASP.NET Core Web API, .NET 8, C#
- **Persistence**: Entity Framework Core 8, PostgreSQL 16, Npgsql
- **Authentication**: ASP.NET Core Identity, JWT bearer tokens, rotating refresh tokens
- **Validation**: FluentValidation
- **Email**: Resend integration with disabled and in-memory development implementations
- **API Documentation**: Swagger / OpenAPI
- **Frontend**: Next.js 16, React 19, TypeScript
- **UI**: Tailwind CSS 4, shadcn/ui, Radix UI, lucide-react
- **Testing**: xUnit, FluentAssertions, ASP.NET Core integration testing, EF Core InMemory
- **Packaging**: NuGet, npm
- **Containerization**: Docker

## Architecture

Rendezvous is a modular monolith with explicit project boundaries. The backend keeps domain rules, application logic, infrastructure concerns, and HTTP hosting separate.

```text
src/
  Rendezvous.Domain/           Core entities, enums, and domain rules
  Rendezvous.Application/      Application contracts, use-case helpers, validation
  Rendezvous.Infrastructure/   EF Core, PostgreSQL, Identity, persistence services
  Rendezvous.Api/              ASP.NET Core API host, controllers, auth, Swagger
  Rendezvous.Web/              Next.js frontend application
tests/
  Rendezvous.Tests/            Backend unit and integration tests
```

### Backend

`Rendezvous.Api` is the HTTP boundary. It configures authentication, authorization, Swagger, application services, infrastructure services, controllers, email delivery, notification writing, media storage, and appointment expiration behavior.

`Rendezvous.Domain` owns the core model. It contains business, service, staff, membership, appointment, invitation, notification, and user-related domain types. Business-level access is modeled through business memberships instead of global account roles.

`Rendezvous.Application` contains application-facing contracts and validation. It depends on the domain layer and keeps use-case definitions separate from persistence and HTTP details.

`Rendezvous.Infrastructure` owns database persistence and external infrastructure concerns. It contains the EF Core `AppDbContext`, entity configuration, migrations, Identity integration, PostgreSQL access, and development data seeding.

### Frontend

`Rendezvous.Web` is a Next.js app using the App Router. It contains public discovery pages, auth pages, customer dashboard pages, employee pages, owner management pages, and admin pages.

The frontend calls the API through typed helper modules under `src/lib`. Reusable UI lives under `src/components`, with shadcn/ui primitives isolated under `src/components/ui`.

### Authorization Model

Rendezvous separates platform authorization from business authorization:

- Global roles are limited to normal users and admins.
- Business access is granted through active `BusinessMembership` records.
- Employee appointment access also requires an active staff profile linked to the current user.
- Admin visibility uses admin-specific routes and does not automatically grant owner or employee permissions.

### Appointment Model

Appointment requests and approved appointments are intentionally distinct states in the same business workflow. Customers submit requests first. Owners or employees review requests based on their permissions. Approved appointments represent committed appointments and are protected against staff-level overlaps.

## Local Development

Restore and build the backend:

```bash
dotnet restore Rendezvous.slnx
dotnet build Rendezvous.slnx
```

Apply database migrations:

```bash
dotnet ef database update \
  --project src/Rendezvous.Infrastructure/Rendezvous.Infrastructure.csproj \
  --startup-project src/Rendezvous.Api/Rendezvous.Api.csproj
```

Run the API:

```bash
dotnet run --project src/Rendezvous.Api/Rendezvous.Api.csproj --urls http://localhost:5000
```

Install frontend dependencies:

```bash
cd src/Rendezvous.Web
npm install
```

Run the frontend:

```bash
npm run dev
```

Open the web app at `http://localhost:3000`.

## Validation

Run backend tests:

```bash
dotnet test Rendezvous.slnx
```

Run frontend linting:

```bash
cd src/Rendezvous.Web
npm run lint
```

Build the frontend:

```bash
cd src/Rendezvous.Web
npm run build
```

## Project Conventions

- Application code belongs under `src/`.
- Backend tests belong under `tests/`.
- Backend code follows standard .NET naming and formatting conventions.
- Frontend code uses TypeScript, React components, Tailwind CSS, and shadcn/ui primitives.
- Business authorization should be implemented through membership and staff-profile checks, not broad global roles.
- Changes should stay small, explicit, and aligned with the existing project boundaries.
