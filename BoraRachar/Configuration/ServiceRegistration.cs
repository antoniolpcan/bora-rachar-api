using BoraRachar.Data;
using BoraRachar.ErrorHandling;
using BoraRachar.Repositories;
using BoraRachar.Security;
using BoraRachar.Services;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

namespace BoraRachar.Configuration
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBoraRachar(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<MongoDbSettings>().Bind(configuration.GetSection("MongoDbSettings"))
                .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString), "Configure MongoDbSettings:ConnectionString no appsettings.json.")
                .Validate(s => !string.IsNullOrWhiteSpace(s.DatabaseName), "Configure o nome do banco MongoDB.").ValidateOnStart();
            services.AddSingleton<IMongoClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
                var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
                settings.ConnectTimeout = TimeSpan.FromSeconds(10);
                return new MongoClient(settings);
            });
            services.AddScoped<IGroupRepository, GroupRepository>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IExpenseService, ExpenseService>();
            services.AddScoped<ISettlementService, SettlementService>();
            services.AddScoped<GroupAccessFilter>();
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddControllers().AddApplicationPart(typeof(ServiceRegistration).Assembly);
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(o =>
            {
                o.AddSecurityDefinition("GroupToken", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = GroupTokens.HeaderName,
                    Description = "Token secreto retornado na criação do grupo. Não é necessário para criar um grupo."
                });
                o.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "GroupToken" } }] = Array.Empty<string>()
                });
            });
            services.AddCors(o => o.AddDefaultPolicy(p => p
                .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" })
                .AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Retry-After")));
            return services;
        }
    }
}
