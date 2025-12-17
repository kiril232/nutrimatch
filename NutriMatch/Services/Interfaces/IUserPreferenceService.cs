using System.Collections.Generic;
using System.Threading.Tasks;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IUserPreferenceService
    {
        Task<(List<object> preferences, List<int> followedRestaurants)> GetUserPreferencesAsync(string userId);
        Task UpdateTagPreferencesAsync(string userId, List<UserMealPreference> preferences);
        Task<(bool success, bool following)> ToggleFollowRestaurantAsync(string userId, int restaurantId);
    }
}