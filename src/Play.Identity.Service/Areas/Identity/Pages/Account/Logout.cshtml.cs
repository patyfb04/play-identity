using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Play.Identity.Service.Entities;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LogoutModel> _logger;
    private readonly IIdentityServerInteractionService _interaction;

    public LogoutModel(SignInManager<ApplicationUser> signInManager, ILogger<LogoutModel> logger, IIdentityServerInteractionService interaction)
    {
        _signInManager = signInManager;
        _logger = logger;
        _interaction = interaction;
    }

    [BindProperty]
    public string ReturnUrl { get; set; }

    public async Task<IActionResult> OnGet(string logoutId)
    {
        var context = await _interaction.GetLogoutContextAsync(logoutId);
        if (context?.ShowSignoutPrompt == false)
        {
            return await OnPost(context.PostLogoutRedirectUri);
        }

        // capture PostLogoutRedirectUri into ReturnUrl so the view can render it
        ReturnUrl = context?.PostLogoutRedirectUri;
        return Page();
    }

    public async Task<IActionResult> OnPost(string returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");

        var redirect = returnUrl ?? ReturnUrl;
        if (!string.IsNullOrEmpty(redirect))
        {
            return Redirect(redirect);
        }

        return RedirectToPage("Register");
    }
}