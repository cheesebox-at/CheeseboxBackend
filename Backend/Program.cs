using System.Text;
using Backend.Endpoints;
using Backend.Middleware;
using Backend.Models;
using Backend.Models.Configuration;
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

        
        // Authentication verifies that someone is who they say they are.
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
            
        //Authorization verifies if they have access permission to what they want to access
        builder.Services.AddAuthorization();

        builder.Services.AddMemoryCache();
        builder.Services.AddLogging();
        builder.Services.AddAntiforgery();
        
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (builder.Services is null)
        {
            Console.WriteLine("builder.Services is null on startup. Can only happen in Main startup method.");
            return;
        }
        _services = builder.Services;

        RegisterMongoServices();
        builder.Services.AddSingleton<MongoService>();
        builder.Services.AddSingleton<UserDbService>();
        builder.Services.AddSingleton<RoleDbService>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<UserService>();
        
        var app = builder.Build();

        // Below is Middleware 
        
        
        app.UseHttpsRedirection();

        app.UseMiddleware<JwtFromCookieMiddleware>();
        
        app.UseAuthentication();
        app.UseAuthorization();

        //todo what does Antiforgery even do??
        app.UseAntiforgery();
        
        var apiGroup = app.MapGroup("/api");

        new SessionEndpoint().Register(apiGroup);
        new RoleEndpoint().Register(apiGroup);

        app.Run();
    }

    private static void RegisterMongoServices()
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

            //client.getdatabse automatically creates db if it doesn't exist.
            return client.GetDatabase(config.DatabaseName);
        });
        
        RegisterCollectionService<SessionModel>(
            dbCollectionName: "Sessions",
            additionalIndexes: ["UserId", "RefreshToken"],
            // expireAfterTouple: (TimeSpan.FromDays(builder.Configuration.GetSection("Session").GetValue<int>("ExpireAfterDays")), nameof(SessionModel.ExpireAfter)));
            expireAfterTouple: (TimeSpan.FromSeconds(1), nameof(SessionModel.ExpireAt)));
        
        RegisterCollectionService<UserModel>(
            dbCollectionName: "Users", 
            uniqueIndexName: "Email");

        RegisterCollectionService<RoleModel>(
            dbCollectionName: "Roles");
        
        RegisterCollectionService<DataStoreModel>(
            dbCollectionName: "DataStore");
    }

    private static void RegisterCollectionService<T>(string dbCollectionName, string? uniqueIndexName = null, string[]? additionalIndexes = null,  (TimeSpan ExpireAfter, string ExpireIndexName)? expireAfterTouple = null)
    {
        _services.AddSingleton(x =>
        {
            var db = x.GetRequiredService<IMongoDatabase>();
            //db.getcollection automatically creates collections if it doesn't exist
            var collection = db.GetCollection<T>(dbCollectionName);
            
            //normal Indexes
            if (additionalIndexes is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;

                List<CreateIndexModel<T>> indexModels = [];
                
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
            
            //unique Indexes
            if (uniqueIndexName is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;

                CreateIndexModel<T>[] indexModels =
                [
                    new CreateIndexModel<T>(indexKeys.Descending(uniqueIndexName), new CreateIndexOptions
                    {
                        Name = uniqueIndexName,
                        Unique = true
                    })
                ];

                collection.Indexes.CreateMany(indexModels);
            }
            
            //ExpireAfter Index
            if (expireAfterTouple is not null)
            {
                var indexKeys = Builders<T>.IndexKeys;
                var expireIndexName = expireAfterTouple.Value.ExpireIndexName;

                CreateIndexModel<T>[] indexModels =
                [
                    new CreateIndexModel<T>(indexKeys.Descending(expireIndexName), new CreateIndexOptions
                    {
                        Name = expireIndexName,
                        ExpireAfter = expireAfterTouple.Value.ExpireAfter
                    })
                ];

                collection.Indexes.CreateMany(indexModels);
            }

            return collection;
        });

        Console.WriteLine($"Registered {dbCollectionName}");
    }
}