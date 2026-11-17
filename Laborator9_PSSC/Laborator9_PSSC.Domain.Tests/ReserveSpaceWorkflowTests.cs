using Laborator9_PSSC.Domain.Commands;
using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.Events;
using Laborator9_PSSC.Domain.Operations;
using Laborator9_PSSC.Domain.Repositories;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Domain.Workflows;
using static Laborator9_PSSC.Domain.Tests.TestData;

namespace Laborator9_PSSC.Domain.Tests;

public sealed class ReserveSpaceWorkflowTests
{
    [Fact]
    public void Publish_ShortIndividualReservation_IsConfirmedWithAReminder()
    {
        var reserved = Value(ReserveSpaceWorkflow.Publish(
            new ReserveSpaceCommand("LM10004", "LIB-2-IND-05", "2026-11-12", "11:00", "12:00", "1"), Catalog, [], NobodyBlocked, Now));

        Assert.Equal(ReservationStatus.Confirmed, reserved.Status);
        Assert.Equal(new DateTime(2026, 11, 12, 10, 30, 0), reserved.RemindAt);
        Assert.Equal("LIB-2-IND-05@202611121100", reserved.ReservationCode);
    }

    [Theory]
    [InlineData("LIB-1-GRP-03", "10:00", "13:00", "4")] // over 2 h
    [InlineData("LIB-1-LAB-01", "10:00", "11:00", "4")] // a lab
    public void Publish_LongOrLabReservation_AwaitsApproval(string space, string start, string end, string attendees)
    {
        var reserved = Value(ReserveSpaceWorkflow.Publish(new ReserveSpaceCommand("LM10005", space, "2026-11-13", start, end, attendees), Catalog, [], NobodyBlocked, Now));

        Assert.Equal(ReservationStatus.AwaitingApproval, reserved.Status);
        Assert.Null(reserved.RemindAt);
    }

    [Fact]
    public void Publish_InvalidFields_StopsBeforeTheRules()
    {
        // The space is taken, but validation fails first, so no rule is reported: the error track skips later steps.
        IReadOnlyList<ExistingReservation> existing = [Booking("LIB-2-IND-05", "LM10001", "2026-11-12", "11:00", "12:00")];

        var error = Error(ReserveSpaceWorkflow.Publish(new ReserveSpaceCommand("LM10004", "LIB-2-IND-05", "2026-11-12", "11:00", "12:00", "x"), Catalog, existing, NobodyBlocked, Now));

        Assert.IsType<ReserveSpaceError.Validation>(error);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_SavesOnceAndRejectsTheSameSlotAgain()
    {
        var reservations = new FakeReservations();
        var workflow = new ReserveSpaceWorkflow(new FakeSpaces(), reservations, new FakeStanding(), new FixedClock(Now));
        var command = new ReserveSpaceCommand("LM10008", "LIB-2-IND-05", "2026-11-12", "18:00", "19:00", "1");

        var first = await workflow.ExecuteAsync(command, CancellationToken.None);
        var second = await workflow.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(ReservationStatus.Confirmed, Value(first).Status);
        Assert.IsType<ReservationRuleViolation.SlotTaken>(Assert.Single(Assert.IsType<ReserveSpaceError.RulesViolated>(Error(second)).Violations));
        Assert.Single(reservations.Saved);
    }

    [Fact]
    public async Task ExecuteAsync_RejectedRequest_SavesNothing()
    {
        var reservations = new FakeReservations();
        var workflow = new ReserveSpaceWorkflow(new FakeSpaces(), reservations, new FakeStanding(), new FixedClock(Now));

        await workflow.ExecuteAsync(new ReserveSpaceCommand("LM10008", "LIB-2-IND-05", "2026-11-12", "08:00", "09:00", "1"), CancellationToken.None);

        Assert.Empty(reservations.Saved);
    }

    private sealed class FakeSpaces : IStudySpacesRepository
    {
        public Task<IReadOnlyDictionary<SpaceId, StudySpace>> GetCatalogAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);
    }

    private sealed class FakeReservations : IReservationsRepository
    {
        public List<SpaceReservation.Placed> Saved { get; } = [];

        public Task<IReadOnlyList<ExistingReservation>> GetActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExistingReservation>>([.. Saved.Select(p => new ExistingReservation(p.Space, p.Student, p.Slot))]);

        public Task SaveAsync(SpaceReservation.Placed reservation, CancellationToken cancellationToken)
        {
            Saved.Add(reservation);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStanding : IStudentStandingRepository
    {
        public Task<IReadOnlyDictionary<StudentId, DateTimeOffset>> GetBlockedAsync(CancellationToken cancellationToken) => Task.FromResult(NobodyBlocked);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("Campus", now.Offset, "Campus", "Campus");
    }
}

public sealed class NoShowPolicyTests
{
    [Fact]
    public void BlockedUntil_ThreeNoShowsInThirtyDays_BlocksForAWeekFromTheLatest()
    {
        var until = NoShowPolicy.BlockedUntil([Now.AddDays(-20), Now.AddDays(-9), Now.AddDays(-2)], Now);

        Assert.Equal(Now.AddDays(5), until.Match(value => value, () => DateTimeOffset.MinValue));
    }

    [Fact]
    public void BlockedUntil_OneNoShowOutsideTheWindow_DoesNotBlock()
    {
        var until = NoShowPolicy.BlockedUntil([Now.AddDays(-40), Now.AddDays(-9), Now.AddDays(-2)], Now);

        Assert.IsType<Functional.Option<DateTimeOffset>.None>(until);
    }

    [Theory]
    [InlineData(15, false)] // exactly 15 minutes late: still in time
    [InlineData(16, true)]
    public void IsNoShow_AfterFifteenMinutesWithoutCheckIn_IsTrue(int minutesLate, bool noShow)
    {
        var slot = Slot("2026-11-12", "10:00", "11:00");
        var now = new DateTimeOffset(slot.StartsAt.AddMinutes(minutesLate), Now.Offset);

        Assert.Equal(noShow, NoShowPolicy.IsNoShow(slot, checkedIn: false, now));
    }
}
