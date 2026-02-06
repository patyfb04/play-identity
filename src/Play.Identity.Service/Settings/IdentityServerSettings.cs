using Duende.IdentityServer.Models;
using Play.Identity.Service.Dtos;
using System.Collections.Generic;
using System.Security.Claims;

namespace Play.Identity.Service.Settings
{
    public class IdentityServerSettings
    {
        public IReadOnlyCollection<ApiScope> ApiScopes { get; init; }
        public IReadOnlyCollection<ApiResource> ApiResources { get; init; }
        public IReadOnlyCollection<Client> Clients { get; init; }
        public IReadOnlyCollection<IdentityResource> IdentityResources { get; init; }
    }
}
