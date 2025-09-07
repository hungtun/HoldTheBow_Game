using System.Text;
using Hobow_Server.Models;
using Hobow_Server.Hubs;
using Hobow_Server.Handlers;
using Hobow_Server.Services;
using Hobow_Server.Middleware;
using Hobow_Server;
using Hobow_Server.Physics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
var settings = new Settings();
builder.Configuration.Bind("Settings", settings);
builder.Services.AddSingleton(settings);
// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DB");

builder.Services.AddDbContext<Hobow_Server.GameDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

builder.Services.AddControllers().AddNewtonsoftJson(o =>
{
    o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    o.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver();
});
builder.Services.AddScoped<IHeroService, HeroService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IEnemyService, EnemyService>();
builder.Services.AddScoped<IEnemyHandler, EnemyHandler>();
// Không cần GameSessionManager nữa - sử dụng ActiveSessionId trong database
builder.Services.AddSingleton<GameState>();


builder.Services.AddSingleton<ServerPhysicsManager>();
builder.Services.AddSingleton<TiledMapParser>();

builder.Services.AddScoped<IHeroHandler, HeroHandler>();
builder.Services.AddSingleton<IMapDataHandler, MapDataHandler>();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(settings.BearerKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
    };

    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/hubs/hero") || path.StartsWithSegments("/hubs/enemy")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{

}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Thêm Session Validation Middleware
app.UseMiddleware<SessionValidationMiddleware>();
app.MapControllers();
app.MapHub<HeroHub>("/hubs/hero");
app.MapHub<EnemyHub>("/hubs/enemy");

Console.WriteLine("[Program] Game server initialized");

var physicsManager = app.Services.GetRequiredService<ServerPhysicsManager>();
var mapParser = app.Services.GetRequiredService<TiledMapParser>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

var mapsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Maps");
if (Directory.Exists(mapsDirectory))
{
    var mapFiles = Directory.GetFiles(mapsDirectory, "*.tmj");
    foreach (var mapFile in mapFiles)
    {
        var mapId = Path.GetFileNameWithoutExtension(mapFile);
        var collisions = mapParser.ParseMapFile(mapFile);
        physicsManager.LoadMapCollisions(mapId, collisions);
    }
    logger.LogInformation($"[Program] Loaded {mapFiles.Length} maps into physics system");
}

// Initialize enemies from database
var enemyInitTask = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var enemyHandler = scope.ServiceProvider.GetRequiredService<IEnemyHandler>();
    await enemyHandler.InitializeEnemiesAsync();
});

var enemyAIUpdateTask = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var enemyHandler = scope.ServiceProvider.GetRequiredService<IEnemyHandler>();

    while (true)
    {
        try
        {
            await enemyHandler.UpdateEnemyAIAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnemyAI] Error: {ex.Message}");
        }

        await Task.Delay(100);
    }
});


var physicsUpdateTask = Task.Run(async () =>
{
    var physics = app.Services.GetRequiredService<ServerPhysicsManager>();
    while (true)
    {
        physics.Update(1f / 60f);
        await Task.Delay(16);
    }
});



app.Run();