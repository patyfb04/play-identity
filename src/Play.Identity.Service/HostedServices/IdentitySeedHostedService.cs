using MassTransit;
using MassTransit.Transports;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Play.Identity.Contracts;
using Play.Identity.Service.Entities;
using Play.Identity.Service.Settings;
using System.Threading;

namespace Play.Identity.Service.HostedServices
{
    public class IdentitySeedHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IdentitySettings _settings;

        public IdentitySeedHostedService(
            IServiceScopeFactory serviceScopeFactory,
            IOptions<IdentitySettings> settings)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _settings = settings.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            await CreateAdminUserAndRoles(roleManager, userManager, publishEndpoint, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task CreateAdminUserAndRoles(
            RoleManager<ApplicationRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IPublishEndpoint publishEndpoint,
            CancellationToken cancellationToken)
        {
            // Create roles unconditionally (MongoDB will create the collection on write)
            await CreateRoleSafeAsync(Roles.Admin, roleManager);
            await CreateRoleSafeAsync(Roles.Player, roleManager);

            // Create admin user if missing
            var adminUser = await userManager.FindByEmailAsync(_settings.AdminUserEmail);
            if (adminUser is null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = _settings.AdminUserEmail,
                    Email = _settings.AdminUserEmail,
                    EmailConfirmed = true,
                    Gil = 100
                };

                var createResult = await userManager.CreateAsync(adminUser, _settings.AdminUserPassword);

                // Ignore duplicate user errors (idempotent)
                if (!createResult.Succeeded &&
                    !createResult.Errors.Any(e => e.Code == "DuplicateUserName"))
                {
                    throw new Exception($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                }
            }

            // Ensure admin user is in Admin role
            if (!await userManager.IsInRoleAsync(adminUser, Roles.Admin))
            {
                var addRoleResult = await userManager.AddToRoleAsync(adminUser, Roles.Admin);

                // Ignore duplicate role assignment errors
                if (!addRoleResult.Succeeded &&
                    !addRoleResult.Errors.Any(e => e.Code == "UserAlreadyInRole"))
                {
                    throw new Exception($"Failed to add admin user to role: {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
                }
            }

            // Publish UserUpdated so Trading can sync the admin user
            await publishEndpoint.Publish(
                new UserUpdated(adminUser.Id, adminUser.Email!, adminUser.Gil),
                cancellationToken
            );

            Console.WriteLine($"Published UserCreated for admin: {adminUser.Email}");

            // Debug log: print roles for admin user
            var roles = await userManager.GetRolesAsync(adminUser);
            Console.WriteLine($"Roles for {_settings.AdminUserEmail}: {string.Join(",", roles)}");
        }

        private static async Task CreateRoleSafeAsync(
            string role,
            RoleManager<ApplicationRole> roleManager)
        {
            // Try to create the role unconditionally
            var result = await roleManager.CreateAsync(new ApplicationRole { Name = role });

            // Ignore duplicate role errors (idempotent)
            if (!result.Succeeded &&
                !result.Errors.Any(e => e.Code == "RoleAlreadyExists"))
            {
                throw new Exception($"Failed to create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}