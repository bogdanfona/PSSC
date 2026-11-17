using Laborator9_PSSC.Domain.States;
using Laborator9_PSSC.Domain.ValueObjects;
using Laborator9_PSSC.Functional;

namespace Laborator9_PSSC.Domain.Tests;

// Shared fixtures: a fixed clock and a small catalog, so every test reads the same, known world.
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.FromHours(2));

    public static readonly IReadOnlyDictionary<SpaceId, StudySpace> Catalog = new[]
    {
        Space("LIB-2-IND-05", 1),
        Space("LIB-2-IND-06", 1),
        Space("LIB-1-GRP-03", 6),
        Space("LIB-1-LAB-01", 12),
    }.ToDictionary(s => s.Id);

    public static readonly IReadOnlyDictionary<StudentId, DateTimeOffset> NobodyBlocked = new Dictionary<StudentId, DateTimeOffset>();

    public static StudySpace Space(string id, int capacity) => new(Value(SpaceId.Parse(id)), Value(Headcount.Create(capacity)));

    public static StudentId Student(string id) => Value(StudentId.Parse(id));

    public static SpaceId SpaceIdOf(string id) => Value(SpaceId.Parse(id));

    public static TimeSlot Slot(string date, string start, string end) => Value(TimeSlot.Parse(date, start, end));

    public static ExistingReservation Booking(string space, string student, string date, string start, string end) =>
        new(SpaceIdOf(space), Student(student), Slot(date, start, end));

    public static T Value<T, TError>(Result<T, TError> result)
        where T : notnull
        where TError : notnull =>
        Assert.IsType<Result<T, TError>.Ok>(result).Value;

    public static TError Error<T, TError>(Result<T, TError> result)
        where T : notnull
        where TError : notnull =>
        Assert.IsType<Result<T, TError>.Error>(result).Value;
}
