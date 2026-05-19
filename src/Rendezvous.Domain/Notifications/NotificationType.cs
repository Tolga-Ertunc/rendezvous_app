namespace Rendezvous.Domain.Notifications;

public enum NotificationType
{
    General = 1,
    AppointmentRequestCreated = 2,
    AppointmentRequestApproved = 3,
    AppointmentRequestRejected = 4,
    OwnerOnboardingApproved = 5,
    OwnerOnboardingRejected = 6,
    AppointmentCancelled = 7,
    AppointmentExpired = 8,
    AppointmentNoShow = 9
}
