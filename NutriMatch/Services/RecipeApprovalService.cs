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

            await _context.SaveChangesAsync();
            await _notificationService.CreateRecipeNotificationsAsync(recipe);

            await _notificationService.CreateRecipeStatusNotificationAsync(
                recipe.UserId,
                recipe.Title,
                recipeId,
                isAccepted: true
            );

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

            await _context.SaveChangesAsync();

            await _notificationService.CreateRecipeStatusNotificationAsync(
                recipe.UserId,
                recipe.Title,
                recipeId,
                isAccepted: false,
                declineReason: reason
            );

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

                approvedCount++;
            }

            await _context.SaveChangesAsync();

            foreach (var recipe in recipes)
            {
                await _notificationService.CreateRecipeNotificationsAsync(recipe);

                await _notificationService.CreateRecipeStatusNotificationAsync(
                    recipe.UserId,
                    recipe.Title,
                    recipe.Id,
                    isAccepted: true
                );
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
    }
}