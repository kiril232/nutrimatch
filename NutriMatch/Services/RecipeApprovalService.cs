
using NutriMatch.Data;
using NutriMatch.Models;
using Microsoft.EntityFrameworkCore;

namespace NutriMatch.Services
{
    public class RecipeApprovalService : IRecipeApprovalService
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public RecipeApprovalService(
            AppDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<List<Recipe>> GetPendingRecipesAsync()
        {
            return await _context.Recipes
                .Where(r => r.RecipeStatus == "Pending")
                .Include(r => r.User)
                .ToListAsync();
        }

        public async Task<(bool success, string message)> ApproveRecipeAsync(int recipeId)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return (false, "Recipe not found.");
            }

            recipe.RecipeStatus = "Accepted";

            if (recipe.HasPendingIngredients == true)
            {
                var pendingIngredients = recipe.RecipeIngredients
                    .Where(ri => ri.Ingredient.Status == "Pending")
                    .Select(ri => ri.Ingredient);

                foreach (var ingredient in pendingIngredients)
                {
                    ingredient.Status = null;
                }

                recipe.HasPendingIngredients = false;
            }

            var userNotification = await _context.Users.FindAsync(recipe.UserId);

            if (userNotification?.NotifyRecipeAccepted == true)
            {
                var notification = new Notification
                {
                    UserId = recipe.UserId,
                    Type = "RecipeAccepted",
                    Message = $"Great news! Your recipe '{recipe.Title}' has been approved and is now live!",
                    RecipeId = recipeId,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);

                await _notificationService.SendEmailAsync(
                    userNotification.Email,
                    "Your recipe has been approved!",
                    $"<p>Hi {userNotification.UserName},</p><p>{notification.Message}</p>"
                );
            }

            await _context.SaveChangesAsync();
            await CreateRecipeNotificationsAsync(recipe);

            return (true, "Recipe approved successfully.");
        }

        public async Task<(bool success, string message)> DeclineRecipeAsync(int recipeId, string reason, string notes)
        {
            var recipe = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(r => r.Id == recipeId);

            if (recipe == null)
            {
                return (false, "Recipe not found.");
            }

            recipe.RecipeStatus = "Declined";
            recipe.DeclineReason = reason ?? string.Empty;
            recipe.AdminComment = notes ?? string.Empty;

            string notificationMessage = string.IsNullOrEmpty(reason) || reason == "No reason provided."
                ? $"Your recipe '{recipe.Title}' was declined."
                : $"Your recipe '{recipe.Title}' was declined. Reason: {reason}";

            var userNotification = await _context.Users.FindAsync(recipe.UserId);

            if (userNotification?.NotifyRecipeDeclined == true)
            {
                var notification = new Notification
                {
                    UserId = recipe.UserId,
                    Type = "RecipeDeclined",
                    Message = notificationMessage,
                    RecipeId = recipeId,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);

                await _notificationService.SendEmailAsync(
                    userNotification.Email,
                    "Your recipe was declined",
                    $"<p>Hi {userNotification.UserName},</p><p>{notification.Message}</p>"
                );
            }

            await _context.SaveChangesAsync();

            return (true, "Recipe declined successfully.");
        }

        public async Task<(bool success, string message, int approvedCount)> BulkApproveRecipesAsync(List<int> recipeIds)
        {
            if (!recipeIds.Any())
            {
                return (false, "No recipe IDs provided.", 0);
            }

            var recipes = await _context.Recipes
                .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .Where(r => recipeIds.Contains(r.Id))
                .ToListAsync();

            if (!recipes.Any())
            {
                return (false, "No recipes found.", 0);
            }

            int approvedCount = 0;
            foreach (var recipe in recipes)
            {
                recipe.RecipeStatus = "Accepted";

                if (recipe.HasPendingIngredients == true)
                {
                    var pendingIngredients = recipe.RecipeIngredients
                        .Where(ri => ri.Ingredient.Status == "Pending")
                        .Select(ri => ri.Ingredient);

                    foreach (var ingredient in pendingIngredients)
                    {
                        ingredient.Status = null;
                    }

                    recipe.HasPendingIngredients = false;
                }

                var userNotification = await _context.Users.FindAsync(recipe.UserId);
                if (userNotification?.NotifyRecipeAccepted == true)
                {
                    var notification = new Notification
                    {
                        UserId = recipe.UserId,
                        Type = "RecipeAccepted",
                        Message = $"Great news! Your recipe '{recipe.Title}' has been approved and is now live!",
                        RecipeId = recipe.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);

                    await _notificationService.SendEmailAsync(
                        userNotification.Email,
                        "Your recipe has been approved!",
                        $"<p>Hi {userNotification.UserName},</p><p>{notification.Message}</p>"
                    );
                }
                approvedCount++;
            }

            await _context.SaveChangesAsync();

            foreach (var recipe in recipes)
            {
                await CreateRecipeNotificationsAsync(recipe);
            }

            return (true, $"{approvedCount} recipe(s) approved successfully.", approvedCount);
        }

        public async Task<Recipe?> GetRecipeForDeclineAsync(int recipeId)
        {
            return await _context.Recipes
                .Include(r => r.User)
                .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(m => m.Id == recipeId);
        }

        private async Task CreateRecipeNotificationsAsync(Recipe recipe)
        {
            var allPrefs = await _context.UserMealPreferences
                .Include(p => p.User)
                .ToListAsync();

            var userPreferences = allPrefs
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Preferences = g.ToList(),
                    User = g.First().User
                })
                .Where(u => u.UserId != recipe.UserId && u.User.NotifyRecipeMatchesTags)
                .ToList();

            foreach (var userPref in userPreferences)
            {
                var matchingTags = GetMatchingTags(userPref.Preferences, recipe.Protein, recipe.Carbs, recipe.Fat, recipe.Calories);

                if (matchingTags.Any())
                {
                    var tagsText = string.Join(", ", matchingTags);

                    var notification = new Notification
                    {
                        UserId = userPref.UserId,
                        Type = "RecipeMatchesTags",
                        Message = $"New recipe matches your preferences ({tagsText}): {recipe.Title}",
                        RecipeId = recipe.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);

                    await _notificationService.SendEmailAsync(
                        userPref.User.Email,
                        "New recipe matches your preferences",
                        $"<p>Hi {userPref.User.UserName},</p><p>{notification.Message}</p>"
                    );
                }
            }

            await _context.SaveChangesAsync();
        }

        private List<string> GetMatchingTags(List<UserMealPreference> preferences, float protein, float carbs, float fat, float calories)
        {
            var matchingTags = new List<string>();

            foreach (var pref in preferences)
            {
                bool matches = pref.Tag switch
                {
                    "high-protein" => pref.ThresholdValue.HasValue ? protein >= pref.ThresholdValue.Value : protein >= 30,
                    "low-carb" => pref.ThresholdValue.HasValue ? carbs <= pref.ThresholdValue.Value : carbs <= 20,
                    "high-carb" => pref.ThresholdValue.HasValue ? carbs >= pref.ThresholdValue.Value : carbs >= 50,
                    "low-fat" => pref.ThresholdValue.HasValue ? fat <= pref.ThresholdValue.Value : fat <= 15,
                    "high-fat" => pref.ThresholdValue.HasValue ? fat >= pref.ThresholdValue.Value : fat >= 30,
                    "low-calorie" => pref.ThresholdValue.HasValue ? calories <= pref.ThresholdValue.Value : calories <= 300,
                    "high-calorie" => pref.ThresholdValue.HasValue ? calories >= pref.ThresholdValue.Value : calories >= 600,
                    "balanced" => IsBalanced(protein, carbs, fat, calories),
                    _ => false
                };

                if (matches)
                {
                    matchingTags.Add(pref.Tag);
                }
            }

            return matchingTags;
        }

        private bool IsBalanced(float protein, float carbs, float fat, float calories)
        {
            if (calories <= 0) return false;

            float proteinRatio = (protein * 4) / calories * 100;
            float carbRatio = (carbs * 4) / calories * 100;
            float fatRatio = (fat * 9) / calories * 100;

            return proteinRatio >= 20 && proteinRatio <= 35 &&
                carbRatio >= 30 && carbRatio <= 50 &&
                fatRatio >= 20 && fatRatio <= 35;
        }
    }
}