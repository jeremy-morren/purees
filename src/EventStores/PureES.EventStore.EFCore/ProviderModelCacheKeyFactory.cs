using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PureES.EventStore.EFCore;

/// <summary>
/// Model cache factory based on provider type and schema
/// </summary>
[PublicAPI]
internal class ProviderModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        var key = ((EventStoreDbContext)context).GetModelCacheKey();

        return (key.Provider, key.Schema, designTime);
    }
    
    public object Create(DbContext context)
        => Create(context, false);
}