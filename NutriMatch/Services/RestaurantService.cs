using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class RestaurantService : IRestaurantService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMealPlanService _mealPlanService;

        private readonly IMealClassificationService _mealClassificationService;
        private readonly INotificationService _notificationService;

        public RestaurantService(
            AppDbContext context,
            IWebHostEnvironment env,
            IMealPlanService mealPlanService,
            INotificationService notificationService,
            IMealClassificationService mealClassificationService
          )
        {
            _context = context;
            _env = env;
            _mealPlanService = mealPlanService;
            _notificationService = notificationService;
          
            _mealClassificationService = mealClassificationService;
        }

        public async Task<List<Restaurant>> GetAllRestaurantsAsync()
        {
            return await _context.Restaurants.ToListAsync();
        }

        public async Task<(Restaurant restaurant, List<RestaurantMeal> filteredMeals)> GetRestaurantWithFilteredMealsAsync(
            int id,
            int? minCalories,
            int? maxCalories,
            int? minProtein,
            int? maxProtein,
            int? minCarbs,
            int? maxCarbs,
            int? minFat,
            int? maxFat)
        {
            var restaurant = await _context.Restaurants
                .Include(r => r.RestaurantMeals)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (restaurant == null)
            {
                return (null, null);
            }

            var filteredMeals = restaurant.RestaurantMeals
                .Where(r =>
                    (minCalories == null || r.Calories >= minCalories) &&
                    (maxCalories == null || r.Calories <= maxCalories) &&
                    (minProtein == null || r.Protein >= minProtein) &&
                    (maxProtein == null || r.Protein <= maxProtein) &&
                    (minFat == null || r.Fat >= minFat) &&
                    (maxFat == null || r.Fat <= maxFat) &&
                    (minCarbs == null || r.Carbs >= minCarbs) &&
                    (maxCarbs == null || r.Carbs <= maxCarbs)
                )
                .ToList();

            Console.WriteLine($"Total meals for restaurant {id}: {filteredMeals.Count}");

            return (restaurant, filteredMeals);
        }

        public async Task<List<object>> GetRestaurantsAsync()
        {
            return await _context.Restaurants
                .OrderBy(r => r.Name)
                .Select(r => new { id = r.Id, name = r.Name })
                .Cast<object>()
                .ToListAsync();
        }

        public async Task<object?> GetRestaurantAsync(int id)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null)
            {
                return null;
            }

            return new
            {
                id = restaurant.Id,
                name = restaurant.Name,
                imageUrl = restaurant.ImageUrl,
                description = restaurant.Description
            };
        }

        public async Task<List<RestaurantMeal>> GetRestaurantMealsAsync(int restaurantId)
        {
            return await _context.RestaurantMeals
                .Where(m => m.RestaurantId == restaurantId)
                .OrderBy(m => m.ItemName)
                .ToListAsync();
        }

        public async Task<(bool success, string message, int? restaurantId)> AddRestaurantAsync(string name, string description, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Restaurant name is required", null);
            }

            var restaurant = new Restaurant
            {
                Name = name,
                Description = description,
                ImageUrl = imagePath
            };

            _context.Restaurants.Add(restaurant);
            await _context.SaveChangesAsync();

            await CreateRestaurantNotificationsAsync(restaurant);

            return (true, "Restaurant added successfully", restaurant.Id);
        }

        public async Task<(bool success, string message)> EditRestaurantAsync(int id, string name, string description, string? imagePath)
        {
            var restaurant = await _context.Restaurants.FindAsync(id);
            if (restaurant == null)
            {
                return (false, "Restaurant not found");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Restaurant name is required");
            }

            restaurant.Name = name;
            restaurant.Description = description;

            if (!string.IsNullOrEmpty(imagePath))
            {
                if (!string.IsNullOrEmpty(restaurant.ImageUrl))
                {
                    var oldImagePath = Path.Combine(_env.WebRootPath, restaurant.ImageUrl.TrimStart('/'));
                    if (File.Exists(oldImagePath))
                    {
                        File.Delete(oldImagePath);
                    }
                }

                restaurant.ImageUrl = imagePath;
            }

            await _context.SaveChangesAsync();

            return (true, "Restaurant updated successfully");
        }

        public async Task<(bool success, string message)> DeleteRestaurantAsync(int id)
        {
            if (id == 0)
                return (false, "Invalid restaurant ID");

            var restaurant = await _context.Restaurants
                .Include(r => r.RestaurantMeals)
                .Include(r => r.Followers)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (restaurant == null)
                return (false, "Restaurant not found");

            if (restaurant.Followers != null && restaurant.Followers.Any())
            {
                _context.RestaurantFollowings.RemoveRange(restaurant.Followers);
            }

            if (restaurant.RestaurantMeals != null && restaurant.RestaurantMeals.Any())
            {
                foreach (var meal in restaurant.RestaurantMeals)
                {
                    await _mealPlanService.HandleDeletedRestaurantMealAsync(meal.Id);
                }
                _context.RestaurantMeals.RemoveRange(restaurant.RestaurantMeals);
            }

            if (!string.IsNullOrEmpty(restaurant.ImageUrl))
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", restaurant.ImageUrl.TrimStart('/'));
                if (File.Exists(imagePath))
                {
                    try
                    {
                        File.Delete(imagePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to delete image file: {imagePath}. Error: {ex.Message}");
                    }
                }
            }

            _context.Restaurants.Remove(restaurant);
            await _context.SaveChangesAsync();

            return (true, $"Restaurant '{restaurant.Name}' and all related data deleted successfully");
        }

        public async Task<(bool success, string message)> AddRestaurantMealAsync(RestaurantMeal meal)
        {
            if (string.IsNullOrWhiteSpace(meal.ItemName))
            {
                return (false, "Meal name is required");
            }

            if (meal.RestaurantId == null || meal.RestaurantId == 0)
            {
                return (false, "Restaurant is required");
            }

            var restaurant = await _context.Restaurants.FindAsync(meal.RestaurantId);
            if (restaurant == null)
            {
                return (false, "Restaurant not found");
            }

            meal.RestaurantName = restaurant.Name;

            meal.Type = await _mealClassificationService.GenerateMealTypes(meal);

            _context.RestaurantMeals.Add(meal);
            await _context.SaveChangesAsync();

            await CreateMealNotificationsAsync(meal, restaurant);

            return (true, "Meal added successfully");
        }

        public async Task<(bool success, string message)> EditRestaurantMealAsync(RestaurantMeal meal)
        {
            if (meal == null || meal.Id == 0)
                return (false, "Invalid meal");

            var existing = await _context.RestaurantMeals.FindAsync(meal.Id);
            if (existing == null)
                return (false, "Meal not found");

            existing.ItemName = meal.ItemName;
            existing.ItemDescription = meal.ItemDescription;
            existing.Type = await _mealClassificationService.GenerateMealTypes(meal);
            existing.Calories = meal.Calories;
            existing.Protein = meal.Protein;
            existing.Carbs = meal.Carbs;
            existing.Fat = meal.Fat;

            await _context.SaveChangesAsync();

            return (true, "Meal updated successfully");
        }

        public async Task<(bool success, string message)> DeleteRestaurantMealAsync(int id)
        {
            var meal = await _context.RestaurantMeals.FindAsync(id);
            if (meal == null)
            {
                return (false, "Meal not found");
            }

            await _mealPlanService.HandleDeletedRestaurantMealAsync(id);

            _context.RestaurantMeals.Remove(meal);
            await _context.SaveChangesAsync();

            return (true, "Meal deleted successfully");
        }

        private async Task CreateRestaurantNotificationsAsync(Restaurant restaurant)
        {
            var users = await _context.Users
                .Where(u => u.NotifyNewRestaurant)
                .ToListAsync();

            foreach (var user in users)
            {
                var notification = new Notification
                {
                    UserId = user.Id,
                    Type = "NewRestaurant",
                    Message = "New restaurant added: " + restaurant.Name,
                    RecipeId = restaurant.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);

                await _notificationService.SendEmailAsync(
                    user.Email,
                    "New restaurant added!",
                    $"<p>Hi {user.UserName},</p><p>New restaurant added: <b>{restaurant.Name}</b>.</p>"
                );
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreateMealNotificationsAsync(RestaurantMeal meal, Restaurant restaurant)
        {
            var followers = await _context.RestaurantFollowings
                .Include(f => f.User)
                .Where(f => f.RestaurantId == meal.RestaurantId && f.User.NotifyRestaurantNewMeal)
                .Select(f => f.UserId)
                .ToListAsync();

            foreach (var userId in followers)
            {
                var notification = new Notification
                {
                    UserId = userId,
                    Type = "RestaurantNewMeal",
                    Message = $"{restaurant.Name} added a new meal: {meal.ItemName}",
                    RecipeId = restaurant.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _context.Notifications.Add(notification);

                var follower = await _context.Users.FindAsync(userId);
                if (follower != null)
                {
                    await _notificationService.SendEmailAsync(
                        follower.Email,
                        "New meal added",
                        $"<p>Hi {follower.UserName},</p><p>{restaurant.Name} added a new meal: <b>{meal.ItemName}</b>.</p>"
                    );
                }
            }

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
                .Where(u => !followers.Contains(u.UserId) && u.User.NotifyMealMatchesTags)
                .ToList();

            foreach (var userPref in userPreferences)
            {
                var matchingTags = GetMatchingTags(userPref.Preferences, meal.Protein, meal.Carbs, meal.Fat, meal.Calories);

                if (matchingTags.Any())
                {
                    var tagsText = string.Join(", ", matchingTags);

                    var notification = new Notification
                    {
                        UserId = userPref.UserId,
                        Type = "MealMatchesTags",
                        Message = $"New meal matches your preferences ({tagsText}): {meal.ItemName} at {restaurant.Name}",
                        RecipeId = restaurant.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _context.Notifications.Add(notification);

                    await _notificationService.SendEmailAsync(
                        userPref.User.Email,
                        "New meal matches your preferences",
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