using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PureES.EventStore.EFCore;

/// <summary>
/// Mirror <see cref="DbContextOptions"/> for a specific <typeparamref name="TContext"/>.
/// All methods are delegated to the source <see cref="DbContextOptions"/>.
/// Allows sharing the same options between multiple contexts.
/// </summary>
/// <typeparam name="TContext">Context type</typeparam>
internal class MirrorDbContextOptions<TContext> : DbContextOptions where TContext : DbContext
{
    private readonly DbContextOptions _source;

    public MirrorDbContextOptions(DbContextOptions source) => _source = source;

    public override Type ContextType => typeof(TContext);

    public override void Freeze() => _source.Freeze();

    public override bool IsFrozen => _source.IsFrozen;

    public override IEnumerable<IDbContextOptionsExtension> Extensions => _source.Extensions;

    public override TExtension GetExtension<TExtension>() => _source.GetExtension<TExtension>();

    public override TExtension? FindExtension<TExtension>() where TExtension : class =>
        _source.FindExtension<TExtension>();

    public override DbContextOptions WithExtension<TExtension>(TExtension extension) =>
        new MirrorDbContextOptions<TContext>(_source.WithExtension(extension));

    protected override bool Equals(DbContextOptions other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is MirrorDbContextOptions<TContext> mirror)
            return _source.Equals(mirror._source);
        return _source.Equals(other);
    }

    public override bool Equals(object? obj) => ReferenceEquals(this, obj)
                                                || (obj is DbContextOptions options && Equals(options));

    public override int GetHashCode() => _source.GetHashCode();

    #region Not Implemented

    protected override ImmutableSortedDictionary<Type, (IDbContextOptionsExtension Extension, int Ordinal)>
        ExtensionsMap => throw new NotImplementedException();

    #endregion
}