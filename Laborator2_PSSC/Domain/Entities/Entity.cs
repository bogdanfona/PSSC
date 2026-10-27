namespace Laborator2_PSSC.Domain.Entities;

// Entities are equal when they have the same identity, regardless of their other data.
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>> where TId : struct
{
    public TId Id { get; } = id;

    public bool Equals(Entity<TId>? other) => other is not null && Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
}
