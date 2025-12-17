using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IMealKeywordService
    {
        Task<List<MealKeyword>> GetMealKeywordsAsync();
        Task<Dictionary<string, List<string>>> GetKeywordsByTagAsync();
        Task<(bool success, string message)> AddMealKeywordAsync(MealKeyword keyword);
        Task<(bool success, string message)> DeleteMealKeywordAsync(int id);
    }
}