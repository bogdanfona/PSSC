using System.Diagnostics;
using Laborator9_PSSC.Domain.ValueObjects;

namespace Laborator9_PSSC.Domain.States;

/// <summary>
/// The states a reservation request goes through while it is being placed. Closed: the constructor is private.
/// <code>Unvalidated -> Validated -> Checked -> Placed</code>
/// There is no Invalid state: a request that fails validation or a business rule leaves the pipeline as a Result error.
/// </summary>
public abstract record SpaceReservation
{
    private SpaceReservation() { }

    /// <summary>The request exactly as typed by the student.</summary>
    public sealed record Unvalidated(string? StudentId, string? SpaceId, string? Date, string? Start, string? End, string? Attendees)
        : SpaceReservation;

    /// <summary>Every field parsed into a value object, and the space exists in the catalog.</summary>
    public sealed record Validated(StudentId Student, SpaceId Space, TimeSlot Slot, Headcount Attendees) : SpaceReservation;

    /// <summary>Every reservation rule holds (future slot, not blocked, capacity, free slot, daily limit).</summary>
    public sealed record Checked(StudentId Student, StudySpace Space, TimeSlot Slot, Headcount Attendees) : SpaceReservation;

    /// <summary>The reservation exists: confirmed at once, or waiting for the librarian's approval.</summary>
    public sealed record Placed(
        ReservationCode Code,
        StudentId Student,
        SpaceId Space,
        TimeSlot Slot,
        Headcount Attendees,
        ReservationApproval Approval,
        DateTimeOffset PlacedAt) : SpaceReservation;

    public TResult Match<TResult>(
        Func<Unvalidated, TResult> unvalidated,
        Func<Validated, TResult> validated,
        Func<Checked, TResult> @checked,
        Func<Placed, TResult> placed) => this switch
        {
            Unvalidated r => unvalidated(r),
            Validated r => validated(r),
            Checked r => @checked(r),
            Placed r => placed(r),
            _ => throw new UnreachableException(),
        };
}

/// <summary>
/// How a placed reservation was accepted. Its own closed type, so a reminder exists only for a confirmed reservation
/// and a reason only for one awaiting approval - no nullable fields that could disagree with a status flag.
/// </summary>
public abstract record ReservationApproval
{
    private ReservationApproval() { }

    /// <summary>Confirmed automatically; the student is reminded 30 minutes before the start.</summary>
    public sealed record AutoConfirmed(DateTime RemindAt) : ReservationApproval;

    /// <summary>Long or lab reservations wait for a librarian.</summary>
    public sealed record AwaitingApproval(string Reason) : ReservationApproval;
}

/// <summary>A space from the catalog, as the reservation context needs it.</summary>
public sealed record StudySpace(SpaceId Id, Headcount Capacity);

/// <summary>A reservation that already holds a slot; used to find conflicts and the student's daily total.</summary>
public sealed record ExistingReservation(SpaceId Space, StudentId Student, TimeSlot Slot);
