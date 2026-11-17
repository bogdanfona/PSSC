using Laborator9_PSSC.Domain.Errors;
using Laborator9_PSSC.Domain.Operations;
using Laborator9_PSSC.Domain.States;
using static Laborator9_PSSC.Domain.Tests.TestData;

namespace Laborator9_PSSC.Domain.Tests;

public sealed class ReservationValidationTests
{
    [Fact]
    public void Validate_AllFieldsValidAndSpaceKnown_ReturnsValidated()
    {
        var validated = Value(new SpaceReservation.Unvalidated("LM10004", "LIB-2-IND-05", "2026-11-12", "11:00", "12:00", "1").Validate(Catalog));

        Assert.Equal("LM10004", validated.Student.Value);
        Assert.Equal(TimeSpan.FromHours(1), validated.Slot.Duration);
    }

    [Fact]
    public void Validate_EveryFieldInvalid_ReportsOneErrorPerFieldInOrder()
    {
        var errors = Error(new SpaceReservation.Unvalidated("lm1", "MALL-1-IND-01", "12.11.2026", "10:00", "11:00", "zero").Validate(Catalog));

        Assert.Collection(errors,
            e => Assert.IsType<ValidationError.InvalidStudentId>(e),
            e => Assert.IsType<ValidationError.InvalidSpaceId>(e),
            e => Assert.IsType<ValidationError.InvalidTimeSlot>(e),
            e => Assert.IsType<ValidationError.InvalidAttendees>(e));
    }

    [Fact]
    public void Validate_WellFormedButUnknownSpace_ReportsUnknownSpace()
    {
        var errors = Error(new SpaceReservation.Unvalidated("LM10004", "DORM-5-IND-99", "2026-11-12", "11:00", "12:00", "1").Validate(Catalog));

        Assert.Equal("DORM-5-IND-99", Assert.IsType<ValidationError.UnknownSpace>(Assert.Single(errors)).Space.Value);
    }
}
