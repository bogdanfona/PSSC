using Laborator9_PSSC.Domain.ValueObjects;
using static Laborator9_PSSC.Domain.Tests.TestData;

namespace Laborator9_PSSC.Domain.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("LIB-2-IND-05", SpaceType.Individual)]
    [InlineData("dorm-3-grp-12", SpaceType.Group)]
    [InlineData("LIB-1-LAB-01", SpaceType.Lab)]
    public void SpaceIdParse_ValidCode_ReadsTheType(string raw, SpaceType type) =>
        Assert.Equal(type, Value(SpaceId.Parse(raw)).Type);

    [Theory]
    [InlineData("LIB-2-05")]
    [InlineData("MALL-1-IND-01")]
    [InlineData("LIB-6-IND-01")]
    [InlineData("LIB-2-IND-00")]
    public void SpaceIdParse_InvalidCode_IsRejected(string raw) =>
        Assert.IsType<SpaceIdError.InvalidFormat>(Error(SpaceId.Parse(raw)));

    [Fact]
    public void SpaceIdParse_Empty_IsMissing() =>
        Assert.IsType<SpaceIdError.Missing>(Error(SpaceId.Parse("  ")));

    [Theory]
    [InlineData("LM12345", true)]
    [InlineData("lm12345", true)]
    [InlineData("LM1234", false)]
    [InlineData("AB12345", false)]
    [InlineData(null, false)]
    public void StudentIdParse_ChecksTheFormat(string? raw, bool valid) =>
        Assert.Equal(valid, StudentId.Parse(raw) is Functional.Result<StudentId, StudentIdError>.Ok);

    [Theory]
    [InlineData("08:00", "08:30")] // shortest, at opening time
    [InlineData("18:00", "22:00")] // longest, ending at closing time
    public void TimeSlotParse_OnTheBoundaries_IsAccepted(string start, string end) =>
        Assert.NotNull(Value(TimeSlot.Parse("2026-11-12", start, end)));

    [Theory]
    [InlineData("10:15", "11:00", typeof(TimeSlotError.NotOnHalfHour))]
    [InlineData("11:00", "10:00", typeof(TimeSlotError.EndNotAfterStart))]
    [InlineData("07:30", "09:00", typeof(TimeSlotError.OutsideOpeningHours))]
    [InlineData("10:00", "14:30", typeof(TimeSlotError.TooLong))]
    [InlineData("1000", "11:00", typeof(TimeSlotError.InvalidTime))]
    public void TimeSlotParse_BreaksARule_ReturnsThatError(string start, string end, Type error) =>
        Assert.IsType(error, Error(TimeSlot.Parse("2026-11-12", start, end)));

    [Theory]
    [InlineData("10:00", "12:00", "11:00", "13:00", true)]
    [InlineData("10:00", "12:00", "12:00", "13:00", false)] // touching ends do not overlap
    public void TimeSlotOverlapsWith_ComparesTheIntervals(string s1, string e1, string s2, string e2, bool overlaps) =>
        Assert.Equal(overlaps, Slot("2026-11-12", s1, e1).OverlapsWith(Slot("2026-11-12", s2, e2)));

    [Theory]
    [InlineData("0", typeof(HeadcountError.OutOfRange))]
    [InlineData("31", typeof(HeadcountError.OutOfRange))]
    [InlineData("doi", typeof(HeadcountError.NotANumber))]
    public void HeadcountParse_OutsideOneToThirty_IsRejected(string raw, Type error) =>
        Assert.IsType(error, Error(Headcount.Parse(raw)));

    [Fact]
    public void ReservationCode_ForAndParse_RoundTrip()
    {
        var code = ReservationCode.For(SpaceIdOf("LIB-2-IND-05"), Slot("2026-11-12", "11:00", "12:00"));

        Assert.Equal("LIB-2-IND-05@202611121100", code.Value);
        Assert.Equal(code, Value(ReservationCode.Parse(code.Value)));
    }
}
