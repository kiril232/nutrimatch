using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IRecipeApprovalService
    {
        Task<List<Recipe>> GetPendingRecipesAsync();
        Task<(bool success, string message)> ApproveRecipeAsync(int recipeId);
        Task<(bool success, string message)> DeclineRecipeAsync(int recipeId, string reason, string notes);
        Task<(bool success, string message, int approvedCount)> BulkApproveRecipesAsync(List<int> recipeIds);
        Task<Recipe?> GetRecipeForDeclineAsync(int recipeId);
    }
}