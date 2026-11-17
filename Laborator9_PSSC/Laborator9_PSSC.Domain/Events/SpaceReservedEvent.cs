namespace Laborator9_PSSC.Domain.Events;

public enum ReservationStatus
{
    Confirmed,
    AwaitingApproval,
}

/// <summary>
/// Announced when a reservation is placed - success only, there is no failure event. Plain data, so other contexts
/// (notifications for the reminder, usage statistics, the librarian's approval queue) can use it without the domain types.
/// </summary>
public sealed record SpaceReservedEvent(
    string ReservationCode,
    string StudentId,
    string SpaceId,
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    int Attendees,
    ReservationStatus Status,
    DateTime? RemindAt,
    DateTimeOffset ReservedAt);
