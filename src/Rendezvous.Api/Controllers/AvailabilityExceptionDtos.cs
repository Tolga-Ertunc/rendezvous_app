using Rendezvous.Domain.Availability;

namespace Rendezvous.Api.Controllers;

public sealed record AvailabilityExceptionRequest(
    Guid? BusinessId,
    Guid? StaffMemberId,
    string Type,
    DateOnly Date,
    bool IsFullDay,
    string? StartsAt,
    string? EndsAt,
    string? Note,
    bool CancelConflictingAppointments);

public sealed record AvailabilityExceptionResponse(
    Guid Id,
    Guid BusinessId,
    Guid? StaffMemberId,
    string? StaffDisplayName,
    string Type,
    DateOnly Date,
    bool IsFullDay,
    string? StartsAt,
    string? EndsAt,
    string? Note,
    DateTime CreatedAtUtc);

public sealed record AvailabilityExceptionConflictResponse(
    string Message,
    int AppointmentCount,
    IReadOnlyList<AvailabilityExceptionAppointmentResponse> Appointments);

public sealed record AvailabilityExceptionAppointmentResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ServiceName,
    string StaffDisplayName);

public sealed record AvailabilityExceptionValidationResult(
    AvailabilityExceptionType Type,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt,
    string? Note);
