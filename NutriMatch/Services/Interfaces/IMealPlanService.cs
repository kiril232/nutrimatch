using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IMealPlanService
    {
        Task<MealPlanResult> GenerateWeeklyMealPlanAsync(string userId, MealPlanRequest request);
        Task<WeeklyMealPlan> GetMealPlanByIdAsync(int id, string userId);
        Task<List<WeeklyMealPlan>> GetUserMealPlansAsync(string userId);
        Task<bool> DeleteMealPlanAsync(int id, string userId);

        Task<bool> RegenerateMealSlotAsync(int mealSlotId, string userId);
        Task HandleDeletedRecipeAsync(int recipeId);
        Task HandleDeletedRestaurantMealAsync(int restaurantMealId);
    }
}