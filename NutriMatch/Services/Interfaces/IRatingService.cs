using System.Threading.Tasks;

namespace NutriMatch.Services
{
    public interface IRatingService
    {
        Task<(bool success, string message, double averageRating, int totalRatings)> AddOrUpdateRatingAsync(string userId, int recipeId, double rating);
        Task<(bool success, string message, double averageRating, int totalRatings)> RemoveRatingAsync(string userId, int recipeId);
    }
}