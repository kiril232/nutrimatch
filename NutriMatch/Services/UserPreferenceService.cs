using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class UserPreferenceService : IUserPreferenceService
    {
        private readonly AppDbContext _context;

        public UserPreferenceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<object> preferences, List<int> followedRestaurants)> GetUserPreferencesAsync(string userId)
        {
            var preferences = await _context.UserMealPreferences
                .Where(p => p.UserId == userId)
                .Select(p => new
                {
                    tag = p.Tag,
                    thresholdValue = p.ThresholdValue
                })
                .ToListAsync<object>();

            var followedRestaurants = await _context.RestaurantFollowings
                .Where(f => f.UserId == userId)
                .Select(f => f.RestaurantId)
                .ToListAsync();

            return (preferences, followedRestaurants);
        }

        public async Task UpdateTagPreferencesAsync(string userId, List<UserMealPreference> preferences)
        {
            var existing = await _context.UserMealPreferences
                .Where(p => p.UserId == userId)
                .ToListAsync();
            
            _context.UserMealPreferences.RemoveRange(existing);

            foreach (var pref in preferences)
            {
                _context.UserMealPreferences.Add(new UserMealPreference
                {
                    UserId = userId,
                    Tag = pref.Tag,
                    ThresholdValue = pref.ThresholdValue
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<(bool success, bool following)> ToggleFollowRestaurantAsync(string userId, int restaurantId)
        {
            var existing = await _context.RestaurantFollowings
                .FirstOrDefaultAsync(f => f.UserId == userId && f.RestaurantId == restaurantId);

            if (existing != null)
            {
                _context.RestaurantFollowings.Remove(existing);
                await _context.SaveChangesAsync();
                return (true, false);
            }
            else
            {
                _context.RestaurantFollowings.Add(new RestaurantFollowing
                {
                    UserId = userId,
                    RestaurantId = restaurantId,
                });
                await _context.SaveChangesAsync();
                return (true, true);
            }
        }
    }
}