using System.Text;
using Backend.Endpoints;
using Backend.Middleware;
using Backend.Models.Configuration;
using Backend.Models.Product;
using Backend.Models.User;
using Backend.Services;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;

namespace Backend;

internal class Program
{
    private static IServiceCollection? _services;

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
        builder.Services.Configure<MongoConfigurationModel>(builder.Configuration.GetSection("MongoDb"));
        builder.Services.Configure<SessionConfigurationModel>(builder.Configuration.GetSection("Session"));

        // Lade die Kestrel-Konfiguration
        // var kestrelOptions = builder.Configuration.GetSection("Kestrel").Get<KestrelOptions>();

        // // Konfiguriere Kestrel für HTTPS
        // if (kestrelOptions?.Endpoints?.Https != null)
        // {
        //     var httpsEndpoint = kestrelOptions.Endpoints.Https;

        //     builder.WebHost.ConfigureKestrel(options =>
        //     {
        //         options.ListenAnyIP(8080, listenOptions =>
        //         {
        //             listenOptions.UseHttps(httpsEndpoint.Certificate.Path, httpsEndpoint.Certificate.Password);
        //         });
        //     });
        // }

        // Authentication
        builder.Services.AddAuthentication(y =>
            {
                y.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                y.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                y.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer("Bearer", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration.GetSection("Session").GetValue<string>("JwtIssuer"),
                    ValidAudience = builder.Configuration.GetSection("Session").GetValue<string>("JwtAudience"),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration.GetSection("Session").GetValue<string>("JwtKey")))
                };
            });

        // Authorization
        builder.Services.AddAuthorization();

        builder.Services.AddMemoryCache();
        builder.Services.AddLogging();
        builder.Services.AddAntiforgery();

        if (builder.Services is null)
        {
            Console.WriteLine("builder.Services is null on startup. Can only happen in Main startup method.");
            return;
        }
        _services = builder.Services;

        RegisterMongoServices(builder);
        builder.Services.AddSingleton<MongoService>();
        builder.Services.AddSingleton<ProductDbService>();
        builder.Services.AddSingleton<UserDbService>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<UserService>();

        var app = builder.Build();

        // Middleware
        app.UseHttpsRedirection();

        app.UseMiddleware<JwtFromCookieMiddleware>();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        var apiGroup = app.MapGroup("/api");

        new ProductEndpoint().Register(apiGroup);
        new SessionEndpoint().Register(apiGroup);

        app.Run();
    }

    private static void RegisterMongoServices(WebApplicationBuilder builder)
    {
        _services!.AddSingleton<IMongoClient>(x =>
        {
            var configService = x.GetRequiredService<IOptions<MongoConfigurationModel>>().Value;
            var settings = MongoClientSettings.FromConnectionString(configService.ConnectionUri);
            return new MongoClient(settings);
        });

        _services!.AddSingleton<IMongoDatabase>(x =>
        {
            var client = x.GetRequiredService<IMongoClient>();
            var config = x.GetRequiredService<IOptions<MongoConfigurationModel>>().Value;

            return client.GetDatabase(config.DatabaseName);
        });

        RegisterCollectionService<ProductModel>(
            dbCollectionName: "Products");

        RegisterCollectionService<SessionModel>(
            dbCollectionName: "Sessions",
            additionalIndexes: new[] { "UserId", "RefreshToken" },
            expireAfterTouple: (TimeSpan.FromDays(builder.Configuration.GetSection("Session").GetValue<int>("ExpireAfterDays")), nameof(SessionModel.ExpireAt)));

        RegisterCollectionService<UserModel>(
            dbCollectionName: "Users",
            uniqueIndexName: "Email");
    }

    private static void RegisterCollectionService<T>(string dbCollectionName, string? uniqueIndexName = null, string[]? additionalIndexes = null, (TimeSpan ExpireAfter, string ExpireIndexName)? expireAfterTouple = null)
    {
        _services.AddSingleton(x =>
        {
            var db = x.GetRequiredService<IMongoDatabase>();
            var collection = db.GetCollection<T>(dbCollectionName);

            if (additionalIndexes is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;

                var indexModels = new List<CreateIndexModel<T>>();

                foreach (var index in additionalIndexes)
                {
                    indexModels.Add(
                        new CreateIndexModel<T>(indexKeys.Descending(index), new CreateIndexOptions
                        {
                            Name = index
                        }));
                }

                collection.Indexes.CreateMany(indexModels);
            }

            if (uniqueIndexName is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;

                CreateIndexModel<T>[] indexModels =
                {
                    new CreateIndexModel<T>(indexKeys.Descending(uniqueIndexName), new CreateIndexOptions
                    {
                        Name = uniqueIndexName,
                        Unique = true
                    })
                };

                collection.Indexes.CreateMany(indexModels);
            }

            if (expireAfterTouple is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;
                var expireIndexName = expireAfterTouple.Value.ExpireIndexName;

                CreateIndexModel<T>[] indexModels =
                {
                    new CreateIndexModel<T>(indexKeys.Descending(expireIndexName), new CreateIndexOptions
                    {
                        Name = expireIndexName,
                        ExpireAfter = expireAfterTouple.Value.ExpireAfter
                    })
                };

                collection.Indexes.CreateMany(indexModels);
            }

            Console.WriteLine($"Registered {dbCollectionName}");

            return collection;
        });
    }
}

public class EndpointsOptions
{
    public HttpsOptions? Https { get; set; }
}

public class HttpsOptions
{
    public string? Url { get; set; }
    public CertificateOptions? Certificate { get; set; }
}

public class CertificateOptions
{
    public string? Path { get; set; }
    public string? Password { get; set; }
}