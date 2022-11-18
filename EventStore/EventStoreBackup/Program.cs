using System.Security.Cryptography.X509Certificates;
using CommandLine;
using EventStoreBackup;
using EventStoreBackup.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

if (Parser.Default.ParseArguments<CommandLineOptions>(args) is not Parsed<CommandLineOptions> parsed)
    return 1;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var options = parsed.Value;

    var builder = WebApplication.CreateBuilder(args);

    if (options.Config != null)
    {
        Log.Information("Using JSON configuration {Path}", options.Config);
        builder.Configuration.AddJsonFile(options.Config, false, true);
    }
    
    builder.Configuration.AddEnvironmentVariables("CONFIG_");

    builder.Services.AddControllers();

    builder.Services.AddHealthChecks();

    const string scheme = "Basic";

    builder.Services.AddAuthentication(scheme)
        .AddBasic(scheme, builder.Configuration.GetSection("Authentication").Bind);

    builder.Services.AddAuthorization(o =>
    {
        //Only add the policy if auth is enabled
        if (!options.DisableAuth)
            o.FallbackPolicy = new AuthorizationPolicyBuilder(scheme)
                .RequireAuthenticatedUser()
                .RequireRole("$admins")
                .Build();
    });

    builder.Services.AddTransient<BackupService>();

    builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection("Backup").Bind)
        .AddOptions<BackupOptions>().Validate(o =>
        {
            o.Validate();
            return true;
        });

    builder.Host.UseSerilog((context, services, conf) => conf
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console());

    var app = builder.Build();

    app.UseSerilogRequestLogging(context =>
    {
        context.GetLevel = (c, _, _) =>
            c.Request.Path.ToString().StartsWith("/healthz")
                ? LogEventLevel.Debug
                : LogEventLevel.Information;
    });

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/healthz-live").AllowAnonymous();

    app.MapControllers();

    app.Run();

    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Error running web host");
    return 2;
}
finally
{
    Log.CloseAndFlush();
}