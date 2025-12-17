using System.Collections.Generic;
using System.Threading.Tasks;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IRestaurantService
    {
        Task<List<Restaurant>> GetAllRestaurantsAsync();
        Task<(Restaurant restaurant, List<RestaurantMeal> filteredMeals)> GetRestaurantWithFilteredMealsAsync(
            int id, 
            int? minCalories, 
            int? maxCalories, 
            int? minProtein, 
            int? maxProtein, 
            int? minCarbs, 
            int? maxCarbs, 
            int? minFat, 
            int? maxFat);
        Task<List<object>> GetRestaurantsAsync();
        Task<object?> GetRestaurantAsync(int id);
        Task<List<RestaurantMeal>> GetRestaurantMealsAsync(int restaurantId);
        Task<(bool success, string message, int? restaurantId)> AddRestaurantAsync(string name, string description, string imagePath);
        Task<(bool success, string message)> EditRestaurantAsync(int id, string name, string description, string? imagePath);
        Task<(bool success, string message)> DeleteRestaurantAsync(int id);
        Task<(bool success, string message)> AddRestaurantMealAsync(RestaurantMeal meal);
        Task<(bool success, string message)> EditRestaurantMealAsync(RestaurantMeal meal);
        Task<(bool success, string message)> DeleteRestaurantMealAsync(int id);
    }
}