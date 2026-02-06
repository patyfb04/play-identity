using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Play.Identity.Contracts;
using Play.Identity.Service.Dtos;
using Play.Identity.Service.Entities;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Play.Identity.Service.Controllers
{
    [ApiController]
    [Route("users")]
    [Authorize(Policy = LocalApi.PolicyName, Roles = Roles.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPublishEndpoint _publishEndpoint;
        public UsersController(
            UserManager<ApplicationUser> userManager, 
            IPublishEndpoint publishEndpoint)
        {
            _userManager = userManager;
            _publishEndpoint = publishEndpoint;
        }

        [HttpGet]
        public ActionResult <IEnumerable<UserDto>> Get()
        {
            var users = _userManager.Users.ToList().Select(c => c.AsDto());
            return Ok(users);
        }


        [HttpGet("{id}")]
        public async Task <ActionResult<UserDto>> GetByIdAsync(Guid Id)
        {
            var user = await _userManager.FindByIdAsync(Id.ToString());
            if (user is null)
            {
                return NotFound();
            }
                return Ok(user);
        }


        [HttpPut("{id}")]
        public async Task<ActionResult> PutAsync(Guid Id, UpdateUserDto userDto) 
        {

            var user = await _userManager.FindByIdAsync(Id.ToString());
            if (user is null)
            {
                return NotFound();
            }
           
            user.Email = userDto.Email;
            user.UserName = userDto.Email;
            user.Gil = userDto.Gil;

            await _userManager.UpdateAsync(user);

            await _publishEndpoint.Publish(new UserUpdated(user.Id, user.Email, user.Gil));

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAsync(Guid Id) 
        {
            var user = await _userManager.FindByIdAsync(Id.ToString());
            if (user is null)
            {
                return NotFound();
            }
            await _userManager.DeleteAsync(user);

            await _publishEndpoint.Publish(new UserUpdated(user.Id, user.Email, 0));

            return NoContent();
        }
    }
}
