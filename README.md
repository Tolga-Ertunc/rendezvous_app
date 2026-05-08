<img width="995" height="651" alt="Screenshot 2026-05-08 at 10 59 00" src="https://github.com/user-attachments/assets/f4178b34-8e7b-4149-8a03-e6f8c402ea1b" />

# Rendezvous

Rendezvous is a web-based appointment booking platform for businesses and their customers. The first product focus is barber shops, but the core model is intentionally designed around generic businesses, services, staff members, working hours, and appointments so the platform can later support other appointment-driven business types.

## Project Overview

The application helps customers discover approved businesses, review their services and prices, choose a date and time, select a specific available staff member, and submit an appointment request. A real appointment is created only after the business approves that request.

For the first version, guests can browse public business pages and active services, but cannot book appointments. Customers must have an active account before they can request or manage appointments.

Businesses manage their own services, prices, staff, weekly working hours, staff availability, breaks, exceptions, and appointment approvals. Employees can approve or reject their own appointment requests, while business owners can manage appointments and settings across their business.

## Product Direction

Rendezvous starts with barber shops because the scheduling rules are concrete and easy to validate: services have durations, staff members may have different service prices or durations, and appointment conflicts must be prevented for approved appointments.

The system should remain business-type aware without becoming over-abstract too early. The initial domain should use generic names such as `Business`, `BusinessService`, `StaffMember`, `Appointment`, and `BusinessMembership`, while keeping the actual MVP behavior optimized for barber shop booking.

The expected customer booking flow is:

1. Select a business.
2. Select a service.
3. Select a date.
4. Review available time slots.
5. Select a specific available staff member.
6. Submit an appointment request.

The frontend starts this flow with a public business discovery area at `/`. The legacy `/businesses` route redirects to the same discovery experience for old links. It reads from public API endpoints only and shows approved businesses with active services.

Available appointment slots are public for approved businesses and active services, so guests can review dates, times, and available staff before creating an account. Guests still cannot create appointment requests. If a guest selects a staff member for a slot, the UI routes them to registration instead of sending a booking request. A signed-in customer can submit a pending appointment request from an available slot and staff member, but the appointment is not real until the business approves it.

Pending requests do not block a slot. Multiple customers may request the same staff member and time. When one request is approved, the system must reject or prevent approval of overlapping requests for the same staff member.

Employees can review pending appointment requests assigned to their own active staff profile from the dashboard. Business owners can review pending appointment requests across their business from the owner business detail view. Approving a request creates the real appointment by changing it to approved; overlapping pending requests for the same staff member and time are automatically rejected in the same transaction.

Approved appointments can be cancelled by either side until one hour before the appointment start time. Pending requests can be cancelled by the customer at any time. If a pending request reaches its appointment start time without approval, it should expire or be rejected automatically.

Customers can register from `/register`, sign in from `/login`, and review their own appointment requests and appointments from the dashboard. Pending requests and approved appointments that still satisfy the one-hour cutoff can be cancelled by the customer; rescheduling remains out of scope and should be handled by cancelling and booking again.

Pending appointment requests expire once their start time is reached without approval. The current implementation performs this expiration when the main appointment request and appointment list endpoints are read; a background worker can replace or complement that later if operational needs require it.

Employees can now review their own approved upcoming appointments from the dashboard and cancel approved appointments until one hour before the start time. Employee access is scoped to the current user's active staff profile and active employee membership.

Existing owners can create another business from the dashboard. Normal customer accounts cannot create businesses directly. New businesses start as `PendingApproval`, create an active owner membership for the current owner user, and create a staff profile for that owner so the business can be configured immediately. First-time business owner onboarding should be handled through a separate approval/application flow rather than the normal customer dashboard. Owners can manage the operational basics for their business: services, staff display names and active state, business weekly working hours, staff weekly working hours, pending appointment requests, approved upcoming appointments, and employee invitations. The first working-hours editor intentionally supports one interval per day; multiple intervals, breaks, holidays, and leave management remain part of the future scheduling upgrade.

## Main User Types

The platform has four main user types:

- Customer: searches businesses, views public business pages, books appointments, and manages their own appointment requests.
- Employee: sees and manages their own appointment requests and approved appointments.
- Business owner: manages business settings, services, staff members, availability, appointment approvals, and employee invitations.
- Admin: approves businesses, can suspend businesses or users, can manage role assignments, and can perform controlled appointment overrides.

A user can be both a customer and a business member. Global account roles are intentionally small: `Admin` is for system-wide administration and `User` is the normal account role. Business-level permissions are modeled through `BusinessMembership` records instead of global account types.

`BusinessMembership` is the authorization source for business-specific access. A membership connects one user to one business with a business role such as `Owner` or `Employee`, and can be active or suspended. Employee appointment access also requires an active `StaffMember` record for the same user and business, so a business membership alone does not make someone bookable staff. The `Business.OwnerUserId` field remains as the primary owner/creator reference, but business management permissions should be checked through memberships. Admin users are not automatically business owners.

Employee invitations are modeled with `BusinessInvitation`. In the current development version, owners create an invitation for an email address and receive a one-time acceptance token in the UI. The token is stored only as a hash in the database. Email delivery is intentionally not wired yet, so accepting an invitation is done from `/invitations/accept` while signed in with the invited email address.

Admin business visibility is intentionally separate from owner management. Admin accounts can use dedicated admin endpoints to list and inspect all businesses, including pending or suspended records, but that does not grant access to owner routes or make the admin account a business member. Admins can also change a business status to approved, suspended, or rejected from the admin business detail view.

Owner and admin business detail views expose read-only service and staff lists. Staff members are business-scoped records and are separate from global user roles; a user can be represented as staff for one business without changing their global account role. Employee dashboard actions are scoped to appointment requests whose `StaffMember.UserId` matches the current user.

Admin business management includes search and status filtering, summary counts, owner information, and business status actions. Admin user management starts read-only with user search, global roles, and business membership visibility; user suspension and role mutation are intentionally later tasks.

## Scheduling Principles

Scheduling must be based on service duration, business working hours, staff working hours, availability exceptions, and existing approved appointments.

The first availability schema stores one weekly working interval per business and per staff member for each active day. Missing days are treated as closed or unavailable. Overnight shifts are intentionally out of scope for the first scheduling pass.

Known scheduling limitation: MVP working hours remain a single interval per day. This should be revisited before supporting businesses that need split shifts, lunch breaks, official holidays, staff leave, or irregular schedules.

The first slot calculation uses 15-minute steps, intersects business and staff weekly working hours, applies the selected service duration, and excludes staff members with overlapping approved appointments. Appointment request creation and approval remain separate steps.

The database protects against overlapping approved appointments for the same staff member with a PostgreSQL exclusion constraint. Application-level checks and serializable approval transactions are still used for user experience and controlled status changes, but the database is the final guard for approved appointment conflicts.

Turkey is the initial market. Appointment display should use the Turkey timezone, but stored appointment timestamps should be UTC to avoid future time handling issues.

## Technical Direction

The backend is a modular ASP.NET Core Web API monolith. It should keep clear boundaries between domain model, application logic, infrastructure, and API hosting.

Authentication uses ASP.NET Core Identity for users and password hashing, JWT Bearer access tokens for API authorization, and rotating refresh tokens stored server-side as hashes. Registration creates a normal global `User` account and returns an authenticated session. Business owner and employee access are checked through active `BusinessMembership` records, with employee appointment actions additionally scoped to the user's staff profile. The global `Admin` role does not automatically grant owner or employee endpoint access. Admin business visibility uses separate read-only admin routes instead of overriding owner membership checks.

The frontend is a Next.js application using TypeScript, Tailwind CSS, and shadcn/ui. shadcn/ui is installed in the frontend project only; it does not belong in the .NET projects.

The database is PostgreSQL. Entity Framework Core is used for persistence. Docker is not required for the first development pass, but the project structure should remain Docker-friendly.

## Repository Structure

```text
src/
  Rendezvous.Api/              ASP.NET Core Web API host
  Rendezvous.Application/      Use cases, contracts, validation, app services
  Rendezvous.Domain/           Entities, enums, value objects, core rules
  Rendezvous.Infrastructure/   EF Core, PostgreSQL, Identity, email, integrations
  Rendezvous.Web/              Next.js, TypeScript, Tailwind, shadcn/ui frontend
tests/
  Rendezvous.Tests/            Backend test project
```

## Tech Stack

- Backend: ASP.NET Core Web API on .NET 8
- Language: C#
- ORM: Entity Framework Core 8
- Database: PostgreSQL 16
- Authentication: ASP.NET Core Identity, JWT Bearer, rotating refresh tokens
- Frontend: Next.js 16, React 19, TypeScript
- Styling/UI: Tailwind CSS 4, shadcn/ui, lucide-react
- Tests: xUnit, ASP.NET Core integration testing, EF Core InMemory for API test isolation
- Package managers: NuGet for .NET, npm for frontend
