using System.Collections.Generic;
using System.Threading.Tasks;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IRecipeService
    {
        Task<(List<Recipe> recipes, int totalRecipes)> GetPaginatedRecipesAsync(int page, int pageSize);
        Task<List<int>> GetUserFavoriteRecipeIdsAsync(string userId);
        Task<Recipe> GetRecipeByIdAsync(int id);
        Task<(double averageRating, int totalRatings, double userRating, bool hasUserRated)> GetRatingDataAsync(int recipeId, string userId = null);
        Task<bool> IsRecipeFavoritedAsync(string userId, int recipeId);
        Task<List<Recipe>> GetUserRecipesAsync(string userId);
        Task<double> GetUserAverageRatingAsync(string userId);
        Task<(Recipe recipe, float totalCalories, float totalProtein, float totalCarbs, float totalFat, bool hasPendingIngredients)> CalculateRecipeNutritionAsync(Recipe recipe, List<SelectedIngredient> ingredients);
        Task<Recipe> CreateRecipeAsync(Recipe recipe, List<SelectedIngredient> ingredients, string imageUrl);
        Task<Recipe> UpdateRecipeAsync(Recipe recipe, List<SelectedIngredient> ingredients, string imageUrl);
        Task DeleteRecipeAsync(int recipeId);
        float ConvertUnit(float number, string unit);
        Task<(bool success, string message, bool isFavorited)> ToggleFavoriteAsync(string userId, int recipeId);
    }
}