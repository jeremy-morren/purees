using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace PureES.EventStores.Tests.EFCore;

public class EfCoreTests(ITestOutputHelper output)
{
    [Fact]
    public void Crud()
    {
        const string dbName = "efcore_test";
        const string connString = "Host=localhost;Username=postgres;Password=postgres";

        using (var conn = new NpgsqlConnection(connString))
        {
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS {dbName} with (force)";
            cmd.ExecuteNonQuery();
                
            cmd.CommandText = $"CREATE DATABASE {dbName}";
            cmd.ExecuteNonQuery();
        }
        
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql($"{connString};Database={dbName}")
            .Options;
        
        using (var context = new TestDbContext(options))
        {
            context.Database.EnsureCreated();

            output.WriteLine(context.Database.GenerateCreateScript());

            var items = new[]
            {
                Parent.New(5, "1", "2"),
                Parent.New(6, "3"),
                Parent.New(7),
            };
            context.Set<Parent>().AddRange(items);
            context.SaveChanges();
        }
        
        using (var context = new TestDbContext(options))
        {
            var read = context.Set<Parent>().ToList();
            read.Should().HaveCount(3);
            read.Select(x => x.IdLeft).Should().BeEquivalentTo([ 5, 6, 7 ]);
            read[0].Children.Should().HaveCount(2);
            read[1].Children.Should().HaveCount(1);
            read[2].Children.Should().BeEmpty();
            
            read[0].Children.Select(x => x.Name).Should().BeEquivalentTo(["1", "2"]);
            read[1].Children.Select(x => x.Name).Should().BeEquivalentTo(["3"]);

            var cmd = context.Set<Parent>()
                .Where(p => p.Children.Any(c => c.Name == "2"))
                .CreateDbCommand();

            output.WriteLine(cmd.CommandText);
        }
    }
    
    
    [Fact]
    public void Query()
    {
        const string dbName = "efcore_test";
        const string connString = "Host=localhost;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql($"{connString};Database={dbName}")
            .Options;
        
        using (var context = new TestDbContext(options))
        {
            var names = new[] { "2", "3" };
            var cmd = context.Set<Parent>()
                .Where(p => p.Children.Any(c => names.Contains(c.Name)))
                .CreateDbCommand();
            
            output.WriteLine(cmd.CommandText);
        }
    }
    
    private class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Parent>(b =>
            {
                b.HasKey(x => new {x.IdLeft, x.IdRight});
                b.OwnsMany(x => x.Children, c =>
                {
                    c.HasIndex("ParentIdLeft", "ParentIdRight", nameof(Child.Name));
                });
                //.HasKey("Id");
            });
        }
    }
    
    public record Parent
    {
        public required int IdLeft { get; init; }
        
        public required int IdRight { get; init; }
        
        public required ICollection<Child> Children { get; init; }
        
        public static Parent New(int id, params string[] children) => new()
        {
            IdLeft = id,
            IdRight = id * 3,
            Children = children.Select(x => new Child { Name = x }).ToList()
        };
    }
    
    public record Child
    {
        public required string Name { get; init; }
    }
}