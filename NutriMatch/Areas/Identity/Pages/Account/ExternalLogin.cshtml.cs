#nullable disable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using NutriMatch.Models;

namespace NutriMatch.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly IUserStore<User> _userStore;
        private readonly IUserEmailStore<User> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<User> signInManager,
            UserManager<User> userManager,
            IUserStore<User> userStore,
            IEmailSender emailSender,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }
        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(50, ErrorMessage = "The {0} must be at most {1} characters long.")]
            [Display(Name = "Username")]
            public string Username { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                var currentEmail = info.Principal.FindFirstValue(ClaimTypes.Email);

                if (!string.IsNullOrEmpty(currentEmail) && user.Email != currentEmail)
                {
                    _logger.LogInformation($"OAuth email changed from {user.Email} to {currentEmail}");
                    user.Email = currentEmail;
                    user.NormalizedEmail = currentEmail.ToUpper();
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }

                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                ProviderDisplayName = info.ProviderDisplayName;
                ReturnUrl = returnUrl;
                return Page();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {

                var hasPassword = await _userManager.HasPasswordAsync(existingUser);
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }


                var existingLogins = await _userManager.GetLoginsAsync(existingUser);
                var alreadyLinked = existingLogins.Any(l =>
                    l.LoginProvider == info.LoginProvider &&
                    l.ProviderKey == info.ProviderKey);

                if (!alreadyLinked)
                {
                    _logger.LogInformation($"Linking {info.ProviderDisplayName} to existing account with email {email}");
                    var addLoginRes = await _userManager.AddLoginAsync(existingUser, info);

                    if (addLoginRes.Succeeded)
                    {
                        await _signInManager.SignInAsync(existingUser, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }

                    foreach (var error in addLoginRes.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            else
            {
                var username = email.Split('@')[0];
                var uniqueUsername = username;
                var counter = 1;

                while (await _userManager.FindByNameAsync(uniqueUsername) != null)
                {
                    uniqueUsername = $"{username}{counter}";
                    counter++;
                }

                var user = CreateUser();
                user.ProfilePictureUrl = "/images/DefaultProfile.png";
                user.EmailConfirmed = true;

                await _userStore.SetUserNameAsync(user, uniqueUsername, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, email, CancellationToken.None);

                var createResult = await _userManager.CreateAsync(user);

                if (createResult.Succeeded)
                {
                    createResult = await _userManager.AddLoginAsync(user, info);

                    if (createResult.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            if (!string.IsNullOrEmpty(email))
            {
                Input = new InputModel
                {
                    Email = email,
                    Username = email.Split('@')[0]
                };
            }
            return Page();
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            if (ModelState.IsValid)
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(Input.Email);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError(string.Empty, $"An account with {Input.Email} already exists. Please use a different email or login with your existing account.");
                    ProviderDisplayName = info.ProviderDisplayName;
                    ReturnUrl = returnUrl;
                    return Page();
                }

                var existingUserByUsername = await _userManager.FindByNameAsync(Input.Username);
                if (existingUserByUsername != null)
                {
                    ModelState.AddModelError(nameof(Input.Username), "This username is already taken. Please choose another.");
                    ProviderDisplayName = info.ProviderDisplayName;
                    ReturnUrl = returnUrl;
                    return Page();
                }

                var user = CreateUser();
                user.ProfilePictureUrl = "/images/DefaultProfile.png";
                user.EmailConfirmed = true;

                await _userStore.SetUserNameAsync(user, Input.Username, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        private User CreateUser()
        {
            try
            {
                return Activator.CreateInstance<User>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(User)}'.");
            }
        }

        private IUserEmailStore<User> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<User>)_userStore;
        }
    }
}