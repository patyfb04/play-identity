using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Play.Common.MassTransit;
using Play.Common.Settings;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Exceptions;
using Play.Identity.Service.HostedServices;
using Play.Identity.Service.Settings;
using Serilog;


Console.WriteLine($"Starting Identity Service...");


var builder = WebApplication.CreateBuilder(args);

// Logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .CreateLogger();

builder.Host.UseSerilog();

// MongoDB Guid serialization
BsonSerializer.RegisterSerializer(typeof(Guid), new GuidSerializer(GuidRepresentation.Standard));
BsonSerializer.RegisterSerializer(typeof(Guid?), new NullableSerializer<Guid>(new GuidSerializer(GuidRepresentation.Standard)));

// Settings
var rabbitDBSettings = builder.Configuration.GetSection(nameof(RabbitMQSettings)).Get<RabbitMQSettings>();

Console.WriteLine($"RabbitMQ user from config: '{rabbitDBSettings?.Username}'");

const string AllowedOriginsSettings = "AllowedOrigins";
var serviceSettings = builder.Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
var mongoDBSettings = builder.Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
var identityServerSettings = builder.Configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();
var identitySettings = builder.Configuration.GetSection(nameof(IdentitySettings));

Console.WriteLine("MongoDbSettings:");
Console.WriteLine($"  ConnectionString: {mongoDBSettings?.ConnectionString}");
Console.WriteLine($"  DatabaseName: {mongoDBSettings?.DatabaseName}");

try
{
    var client = new MongoClient(mongoDBSettings.ConnectionString);
    var db = client.GetDatabase(mongoDBSettings.DatabaseName);
    Console.WriteLine("Mongo connection test: SUCCESS");
}
catch (Exception ex)
{
    Console.WriteLine("Mongo connection test: FAILED");
    Console.WriteLine(ex.ToString());
}

// Identity + Mongo stores
builder.Services
    .Configure<IdentitySettings>(identitySettings)
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.ClaimsIdentity.RoleClaimType = "role";
    })
    .AddRoles<ApplicationRole>()
    .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>(
        mongoDBSettings.ConnectionString,
        serviceSettings.ServiceName
    );

// MassTransit
builder.Services.AddMassTransitWithRabbitMq(retryConfigurator =>
{
    retryConfigurator.Interval(3, TimeSpan.FromSeconds(5));
    retryConfigurator.Ignore(typeof(UnknownUserException));
    retryConfigurator.Ignore(typeof(InsufficientFundsException));
});

//  Convert appsettings clients → IdentityServer Client objects
var mappedClients = identityServerSettings.Clients.Select(c => new Client
{
    ClientId = c.ClientId,
    ClientName = c.ClientName,

    AllowedGrantTypes = c.AllowedGrantTypes,
    RequireClientSecret = c.RequireClientSecret,
    RequirePkce = c.RequirePkce,

    RedirectUris = c.RedirectUris,
    PostLogoutRedirectUris = c.PostLogoutRedirectUris,
    AllowedCorsOrigins = c.AllowedCorsOrigins,

    AllowedScopes = c.AllowedScopes,
    AlwaysIncludeUserClaimsInIdToken = c.AlwaysIncludeUserClaimsInIdToken,

    ClientSecrets = c.ClientSecrets?
        .Select(s => new Secret(s.Value.Sha256()))
        .ToList()
}).ToList();

// IdentityServer
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseSuccessEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseErrorEvents = true;
})
    .AddAspNetIdentity<ApplicationUser>()
    .AddInMemoryApiScopes(identityServerSettings.ApiScopes)
    .AddInMemoryApiResources(identityServerSettings.ApiResources)
    .AddInMemoryClients(mappedClients)
    .AddInMemoryIdentityResources(identityServerSettings.IdentityResources)
    .AddDeveloperSigningCredential();

// Local API auth
builder.Services.AddLocalApiAuthentication();

// MVC + Razor Pages
builder.Services.AddControllers();
builder.Services.AddRazorPages();

// Seed users/roles
builder.Services.AddHostedService<IdentitySeedHostedService>();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Play.Identity.Service", Version = "v1" });
});

// Cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection(AllowedOriginsSettings)?.Value)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Play.Identity.Service v1"));
}
else
{
    app.UseHttpsRedirection();
}


app.UseStaticFiles();

app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseIdentityServer();

app.MapControllers();
app.MapRazorPages();

app.MapHealthChecks("/health");

app.Run();