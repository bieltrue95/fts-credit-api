using FtsCredit.Api.Common.Extensions;
using FtsCredit.Api.Common.Mapster;
using FtsCredit.Api.Common.Middleware;
using FtsCredit.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/fts-credit-api.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("logs/fts-credit-api.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddRedisCache(builder.Configuration);
    builder.Services.AddRabbitMq(builder.Configuration);
    builder.Services.AddJwtAuth(builder.Configuration);
    builder.Services.AddDomainServices();
    builder.Services.AddHandlers();
    builder.Services.AddValidators();
    builder.Services.AddSwagger();
    builder.Services.AddControllers();

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()));

    MappingConfig.Configure();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
