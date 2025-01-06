using Microsoft.EntityFrameworkCore;

namespace PureES.EventStores.Tests.EFCore;

public class NoOpDbContext : DbContext
{
    public NoOpDbContext(DbContextOptions options)
        : base(options)
    {
    }
}