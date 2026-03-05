using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using MassTransit;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Play.Common.MassTransit;
using Play.Common.Messaging;
using Play.Common.Settings;
using Play.Identity.Service.Entities;
using Play.Identity.Service.HostedServices;
using Play.Identity.Service.Settings;
using Serilog;

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

// -------------------------------------------------------
// REMOVE CosmosDbSettings (Identity does NOT use CosmosDB)
// -------------------------------------------------------
// builder.Services.Configure<CosmosDbSettings>(
//     builder.Configuration.GetSection(nameof(CosmosDbSettings)));

// -------------------------------------------------------
// ADD MongoDbSettings (Identity uses MongoDB)
// -------------------------------------------------------
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(nameof(MongoDbSettings)));

builder.Services.Configure<ServiceSettings>(
    builder.Configuration.GetSection(nameof(ServiceSettings)));

builder.Services.Configure<ServiceBusSettings>(
    builder.Configuration.GetSection(nameof(ServiceBusSettings)));

builder.Services.Configure<MassTransitSettings>(
    builder.Configuration.GetSection(nameof(MassTransitSettings)));

builder.Services.Configure<IdentitySettings>(
    builder.Configuration.GetSection(nameof(IdentitySettings)));

builder.Services.Configure<IdentityServerSettings>(
    builder.Configuration.GetSection(nameof(IdentityServerSettings)));

var serviceSettings = builder.Configuration.GetSection(nameof(ServiceSettings)).Get<ServiceSettings>();
var mongoDBSettings = builder.Configuration.GetSection(nameof(MongoDbSettings)).Get<MongoDbSettings>();
var identityServerSettings = builder.Configuration.GetSection(nameof(IdentityServerSettings)).Get<IdentityServerSettings>();
var identitySettings = builder.Configuration.GetSection(nameof(IdentitySettings)).Get<IdentitySettings>();

// -------------------------------------------------------
// Identity + Mongo stores (correct)
// -------------------------------------------------------
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.ClaimsIdentity.RoleClaimType = "role";
    })
    .AddRoles<ApplicationRole>()
    .AddMongoDbStores<ApplicationUser, ApplicationRole, Guid>(
        mongoDBSettings.ConnectionString,
        serviceSettings.ServiceName
    );

// -------------------------------------------------------
// MassTransit with Azure Service Bus (correct)
// -------------------------------------------------------
builder.Services.AddMassTransitWithAzureServiceBus();

// Convert appsettings clients → IdentityServer Client objects
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
    ClientSecrets = c.ClientSecrets?.Select(s => new Secret(s.Value.Sha256())).ToList()
}).ToList();

// IdentityServer
builder.Services.AddIdentityServer(options =>
{
    options.Events.RaiseSuccessEvents = true;
    options.Events.RaiseFailureEvents = true;
    options.Events.RaiseErrorEvents = true;
    options.IssuerUri = identitySettings.IssuerUri;
})
    .AddAspNetIdentity<ApplicationUser>()
    .AddInMemoryApiScopes(identityServerSettings.ApiScopes)
    .AddInMemoryApiResources(identityServerSettings.ApiResources)
    .AddInMemoryClients(mappedClients)
    .AddInMemoryIdentityResources(identityServerSettings.IdentityResources)
    .AddDeveloperSigningCredential();

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
        policy.WithOrigins(builder.Configuration["AllowedOrigins"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Health checks
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

app.UseForwardedHeaders();

app.UsePathBase("/api/identity");

app.UseStaticFiles();
app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseIdentityServer();

app.MapControllers();
app.MapRazorPages();

// Consistent health endpoint
app.MapGet("/health", () => Results.Ok("Healthy"))
   .AllowAnonymous();

app.Run();