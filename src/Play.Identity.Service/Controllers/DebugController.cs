using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Play.Identity.Service.Entities;

namespace Play.Identity.Service.Controllers
{
    // Minimal controller in Identity service
    [ApiController]
    [Route("debug")]
    public class DebugController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public DebugController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        [HttpGet("claims")]
        [Authorize] // requires auth cookie
        public IActionResult GetClaims()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            return Ok(claims);
        }


        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            var roles = await _userManager.GetRolesAsync(user);
            return Ok(roles);
        }

    }
}
