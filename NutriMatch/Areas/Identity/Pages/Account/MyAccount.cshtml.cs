#nullable disable
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NutriMatch.Models;

namespace NutriMatch.Areas.Identity.Pages.Account
{
    [Authorize]
    public class AccountModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AccountModel> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AccountModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AccountModel> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public ProfileInputModel ProfileInput { get; set; }

        [BindProperty]
        public PasswordInputModel PasswordInput { get; set; }

        [BindProperty]
        public NotificationPreferencesModel NotificationPreferences { get; set; }

        [BindProperty]
        public string ActiveTab { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public string ProfilePictureUrl { get; set; }

        public bool HasPassword { get; set; }

        public class ProfileInputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [Display(Name = "Username")]
            public string UserName { get; set; }

            [Display(Name = "Profile Picture")]
            public IFormFile ProfilePicture { get; set; }
        }

        public class PasswordInputModel
        {
            [DataType(DataType.Password)]
            [Display(Name = "Current Password")]
            public string CurrentPassword { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm New Password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public class NotificationPreferencesModel
        {
            [Display(Name = "Recipe Rated")]
            public bool NotifyRecipeRated { get; set; }

            [Display(Name = "Recipe Accepted")]
            public bool NotifyRecipeAccepted { get; set; }

            [Display(Name = "Recipe Declined")]
            public bool NotifyRecipeDeclined { get; set; }

            [Display(Name = "Restaurant New Meal")]
            public bool NotifyRestaurantNewMeal { get; set; }

            [Display(Name = "Meal Matches Tags")]
            public bool NotifyMealMatchesTags { get; set; }

            [Display(Name = "Recipe Matches Tags")]
            public bool NotifyRecipeMatchesTags { get; set; }

            [Display(Name = "New Restaurant")]
            public bool NotifyNewRestaurant { get; set; }

            [Display(Name = "Meal Plan Updated")]
            public bool NotifyMealPlanUpdated { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!string.IsNullOrEmpty(Request.Query["activeTab"]))
            {
                ActiveTab = Request.Query["activeTab"];
            }
            else if (TempData["ActiveTab"] is string activeTab)
            {
                ActiveTab = activeTab;
                TempData.Remove("ActiveTab");
            }
            else
            {
                ActiveTab = "Account";
            }

            await LoadUserData(user);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            bool hasChanges = false;

            if (ProfileInput.Email != user.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(ProfileInput.Email);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.Email)}", "Email is already taken.");
                    await LoadUserData(user);
                    return Page();
                }

                user.Email = ProfileInput.Email;
                user.NormalizedEmail = _userManager.NormalizeEmail(ProfileInput.Email);
                user.EmailConfirmed = false;
                hasChanges = true;
            }

            if (ProfileInput.UserName != user.UserName)
            {
                var existingUser = await _userManager.FindByNameAsync(ProfileInput.UserName);
                if (existingUser != null && existingUser.Id != user.Id)
                {
                    ModelState.AddModelError($"{nameof(ProfileInput)}.{nameof(ProfileInput.UserName)}", "Username is already taken.");
                    await LoadUserData(user);
                    return Page();
                }

                user.UserName = ProfileInput.UserName;
                user.NormalizedUserName = _userManager.NormalizeName(ProfileInput.UserName);
                hasChanges = true;
            }

            if (ProfileInput.ProfilePicture != null)
            {
                var result = await SaveProfilePictureAsync(ProfileInput.ProfilePicture, user.Id);
                if (!result.Success)
                {
                    StatusMessage = result.ErrorMessage;
                    await LoadUserData(user);
                    return Page();
                }
                hasChanges = true;
            }

            if (hasChanges)
            {
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await LoadUserData(user);
                    return Page();
                }

                await _signInManager.RefreshSignInAsync(user);
                StatusMessage = "Your profile has been updated successfully.";
                _logger.LogInformation("User {UserId} updated profile. New Email: {Email}, New UserName: {UserName}",
                    user.Id, user.Email, user.UserName);
            }
            else
            {
                StatusMessage = "No changes were made to your profile.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            HasPassword = await _userManager.HasPasswordAsync(user);

            if (HasPassword)
            {
                var changePasswordResult = await _userManager.ChangePasswordAsync(user, PasswordInput.CurrentPassword, PasswordInput.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError($"{nameof(PasswordInput)}.{nameof(PasswordInput.CurrentPassword)}", error.Description);
                    }
                    ActiveTab = "Password";
                    await LoadUserData(user);
                    return Page();
                }

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("User changed their password successfully.");
                StatusMessage = "Your password has been changed successfully.";
            }
            else
            {
                var addPasswordResult = await _userManager.AddPasswordAsync(user, PasswordInput.NewPassword);
                if (!addPasswordResult.Succeeded)
                {
                    foreach (var error in addPasswordResult.Errors)
                    {
                        ModelState.AddModelError($"{nameof(PasswordInput)}.{nameof(PasswordInput.NewPassword)}", error.Description);
                    }
                    ActiveTab = "Password";
                    await LoadUserData(user);
                    return Page();
                }

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("User set a password successfully.");
                StatusMessage = "Your password has been set successfully.";
            }

            PasswordInput = new PasswordInputModel();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateNotificationPreferencesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            user.NotifyRecipeRated = NotificationPreferences.NotifyRecipeRated;
            user.NotifyRecipeAccepted = NotificationPreferences.NotifyRecipeAccepted;
            user.NotifyRecipeDeclined = NotificationPreferences.NotifyRecipeDeclined;
            user.NotifyRestaurantNewMeal = NotificationPreferences.NotifyRestaurantNewMeal;
            user.NotifyMealMatchesTags = NotificationPreferences.NotifyMealMatchesTags;
            user.NotifyRecipeMatchesTags = NotificationPreferences.NotifyRecipeMatchesTags;
            user.NotifyNewRestaurant = NotificationPreferences.NotifyNewRestaurant;
            user.NotifyMealPlanUpdated = NotificationPreferences.NotifyMealPlanUpdated;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                ActiveTab = "Notifications";
                await LoadUserData(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User {UserId} updated notification preferences.", user.Id);
            StatusMessage = "Your notification preferences have been updated successfully.";

            TempData["ActiveTab"] = "Notifications";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToPage("/Index");
        }

        private async Task LoadUserData(User user)
        {
            ProfileInput = new ProfileInputModel
            {
                Email = user.Email,
                UserName = user.UserName
            };

            NotificationPreferences = new NotificationPreferencesModel
            {
                NotifyRecipeRated = user.NotifyRecipeRated,
                NotifyRecipeAccepted = user.NotifyRecipeAccepted,
                NotifyRecipeDeclined = user.NotifyRecipeDeclined,
                NotifyRestaurantNewMeal = user.NotifyRestaurantNewMeal,
                NotifyMealMatchesTags = user.NotifyMealMatchesTags,
                NotifyRecipeMatchesTags = user.NotifyRecipeMatchesTags,
                NotifyNewRestaurant = user.NotifyNewRestaurant,
                NotifyMealPlanUpdated = user.NotifyMealPlanUpdated
            };

            ProfilePictureUrl = !string.IsNullOrEmpty(user.ProfilePictureUrl)
                ? user.ProfilePictureUrl
                : GetProfilePictureUrl(user.Id);

            HasPassword = await _userManager.HasPasswordAsync(user);
        }

        private async Task<(bool Success, string ErrorMessage)> SaveProfilePictureAsync(IFormFile file, string userId)
        {
            try
            {
                if (file.Length > 5 * 1024 * 1024)
                {
                    return (false, "Profile picture must be smaller than 5MB.");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return (false, "Please upload a valid image file (JPG, PNG, or GIF).");
                }

                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                Directory.CreateDirectory(uploadsDir);

                await DeleteProfilePictureAsync(userId);

                var fileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.ProfilePictureUrl = $"/images/{fileName}";
                    await _userManager.UpdateAsync(user);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving profile picture for user {UserId}", userId);
                return (false, "An error occurred while saving the profile picture.");
            }
        }

        private async Task DeleteProfilePictureAsync(string userId)
        {
            try
            {
                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                if (Directory.Exists(uploadsDir))
                {
                    var existingFiles = Directory.GetFiles(uploadsDir, $"{userId}_*");
                    foreach (var file in existingFiles)
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile picture for user {UserId}", userId);
            }
        }

        private string GetProfilePictureUrl(string userId)
        {
            try
            {
                var uploadsDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profile-pictures");
                if (Directory.Exists(uploadsDir))
                {
                    var existingFile = Directory.GetFiles(uploadsDir, $"{userId}_*").FirstOrDefault();
                    if (existingFile != null)
                    {
                        var fileName = Path.GetFileName(existingFile);
                        return $"/uploads/profile-pictures/{fileName}";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile picture URL for user {UserId}", userId);
            }

            return "https://via.placeholder.com/120x120/22c55e/ffffff?text=User";
        }
    }
}