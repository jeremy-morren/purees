using Microsoft.EntityFrameworkCore;

namespace PureES.EventStores.Tests.EFCore;

public class EmptyDbContext : DbContext
{
    public EmptyDbContext(DbContextOptions options)
        : base(options)
    {
    }
}