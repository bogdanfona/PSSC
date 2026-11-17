namespace Laborator9_PSSC.Domain.Commands;

/// <summary>The student's request, as raw text: date yyyy-MM-dd, times HH:mm.</summary>
public sealed record ReserveSpaceCommand(string? StudentId, string? SpaceId, string? Date, string? Start, string? End, string? Attendees);
