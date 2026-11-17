using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.Operations;
using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using static Laborator9_PSSC.Domain.Tests.TestData;

namespace Laborator9_PSSC.Domain.Tests;

public sealed class ReservationRulesTests
{
    private static SpaceReservation.Validated Request(string student, string space, string start, string end, int attendees, string date = "2026-11-12") =>
        new(Student(student), SpaceIdOf(space), Slot(date, start, end), Value(Headcount.Create(attendees)));

    [Fact]
    public void CheckRules_ExactlyTwoHoursOfIndividualSpacesInADay_IsAllowed()
    {
        // The daily limit is "at most 2 h": 1 h already booked + 1 h requested is exactly on the boundary.
        IReadOnlyList<ExistingReservation> existing = [Booking("LIB-2-IND-06", "LM10001", "2026-11-12", "09:30", "10:30")];

        var result = Request("LM10001", "LIB-2-IND-05", "10:30", "11:30", 1).CheckRules(Catalog, existing, NobodyBlocked, Now);

        Assert.IsType<Functional.Result<SpaceReservation.Checked, IReadOnlyList<ReservationRuleViolation>>.Ok>(result);
    }

    [Fact]
    public void CheckRules_OverTheDailyLimit_IsRejected()
    {
        IReadOnlyList<ExistingReservation> existing = [Booking("LIB-2-IND-06", "LM10001", "2026-11-12", "09:30", "10:30")];

        var violations = Error(Request("LM10001", "LIB-2-IND-05", "11:00", "12:30", 1).CheckRules(Catalog, existing, NobodyBlocked, Now));

        var limit = Assert.IsType<ReservationRuleViolation.DailyLimitExceeded>(Assert.Single(violations));
        Assert.Equal(TimeSpan.FromHours(1), limit.AlreadyBooked);
    }

    [Fact]
    public void CheckRules_SeveralRulesBroken_ReportsEveryOne()
    {
        IReadOnlyList<ExistingReservation> existing = [Booking("LIB-1-GRP-03", "LM10002", "2026-11-12", "14:00", "16:00")];
        var blocked = new Dictionary<StudentId, DateTimeOffset> { [Student("LM10003")] = Now.AddDays(5) };

        var violations = Error(Request("LM10003", "LIB-1-GRP-03", "15:00", "17:00", 9).CheckRules(Catalog, existing, blocked, Now));

        Assert.Collection(violations,
            v => Assert.IsType<ReservationRuleViolation.StudentBlocked>(v),
            v => Assert.IsType<ReservationRuleViolation.OverCapacity>(v),
            v => Assert.IsType<ReservationRuleViolation.SlotTaken>(v));
    }

    [Fact]
    public void CheckRules_SlotAlreadyStarted_IsRejected() =>
        Assert.IsType<ReservationRuleViolation.StartsInPast>(Assert.Single(
            Error(Request("LM10004", "LIB-2-IND-05", "08:30", "09:30", 1).CheckRules(Catalog, [], NobodyBlocked, Now))));

    [Theory]
    [InlineData("LIB-2-IND-05", 2, typeof(ReservationRuleViolation.IndividualSpaceForOnePerson))]
    [InlineData("LIB-1-GRP-03", 1, typeof(ReservationRuleViolation.GroupSpaceNeedsGroup))]
    public void CheckRules_WrongNumberOfPeopleForTheSpaceType_IsRejected(string space, int attendees, Type violation) =>
        Assert.IsType(violation, Assert.Single(Error(Request("LM10004", space, "11:00", "12:00", attendees).CheckRules(Catalog, [], NobodyBlocked, Now))));

    [Fact]
    public void CheckRules_BlockThatAlreadyEnded_DoesNotCount()
    {
        var blocked = new Dictionary<StudentId, DateTimeOffset> { [Student("LM10003")] = Now.AddMinutes(-1) };

        var result = Request("LM10003", "LIB-2-IND-05", "11:00", "12:00", 1).CheckRules(Catalog, [], blocked, Now);

        Assert.IsType<Functional.Result<SpaceReservation.Checked, IReadOnlyList<ReservationRuleViolation>>.Ok>(result);
    }
}
