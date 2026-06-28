using System.Text;
using FluentValidation;
using FtsCredit.Api.Domain.Interfaces;
using FtsCredit.Api.Domain.Services;
using FtsCredit.Api.Features.CreditRequest.Create;
using FtsCredit.Api.Features.CreditRequest.GetStatus;
using FtsCredit.Api.Features.Receivables.Anticipate;
using FtsCredit.Api.Features.ScoreValidation.ValidateScore;
using FtsCredit.Api.Infrastructure.Cache;
using FtsCredit.Api.Infrastructure.Messaging;
using FtsCredit.Api.Infrastructure.Persistence;
using FtsCredit.Api.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace FtsCredit.Api.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Postgres")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICreditRequestRepository, CreditRequestRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOutboxWriter, OutboxService>();

        return services;
    }

    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.ConnectRetry = 5;
            options.ReconnectRetryPolicy = new ExponentialRetry(2000);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddScoped<ICacheService, RedisCacheService>();
        return services;
    }

    public static IServiceCollection AddRabbitMq(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest"
            };

            var attempts = 0;
            while (true)
            {
                try
                {
                    return factory.CreateConnectionAsync().GetAwaiter().GetResult();
                }
                catch (Exception) when (attempts++ < 5)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(attempts * 2));
                }
            }
        });

        services.AddSingleton<IChannel>(sp =>
        {
            var connection = sp.GetRequiredService<IConnection>();
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            channel.ExchangeDeclareAsync("credit", ExchangeType.Direct, durable: true).GetAwaiter().GetResult();
            return channel;
        });

        services.AddScoped<IEventPublisher, RabbitMqPublisher>();
        services.AddHostedService<OutboxPublisher>();

        return services;
    }

    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        var key = Encoding.UTF8.GetBytes(config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured"));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<IScoreEngine, ScoreEngine>();
        return services;
    }

    public static IServiceCollection AddHandlers(this IServiceCollection services)
    {
        services.AddScoped<CreateCreditRequestHandler>();
        services.AddScoped<GetCreditStatusHandler>();
        services.AddScoped<ValidateScoreHandler>();
        services.AddScoped<AnticipateReceivablesHandler>();
        return services;
    }

    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddScoped<IValidator<CreateCreditRequestCommand>, CreateCreditRequestValidator>();
        services.AddScoped<IValidator<ValidateScoreCommand>, ValidateScoreValidator>();
        services.AddScoped<IValidator<AnticipateReceivablesCommand>, AnticipateReceivablesValidator>();
        return services;
    }

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "FTS Credit API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "Informe o token JWT: Bearer {token}",
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }
}
