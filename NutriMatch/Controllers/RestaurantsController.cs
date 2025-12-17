using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriMatch.Models;
using NutriMatch.Services;

namespace NutriMatch.Controllers
{
    public class RestaurantsController : Controller
    {
        private readonly IRestaurantService _restaurantService;
        private readonly IMealClassificationService _mealClassificationService;
        private readonly IUserPreferenceService _userPreferenceService;

        public RestaurantsController(
            IRestaurantService restaurantService,
            IMealClassificationService mealClassificationService,
            IUserPreferenceService userPreferenceService)
        {
            _restaurantService = restaurantService;
            _mealClassificationService = mealClassificationService;
            _userPreferenceService = userPreferenceService;
        }

        public async Task<IActionResult> Index()
        {
            var restaurants = await _restaurantService.GetAllRestaurantsAsync();
            return View(restaurants);
        }

        public async Task<IActionResult> GetRestaurantMeals(
            int? id,
            int? minCalories,
            int? maxCalories,
            int? minProtein,
            int? maxProtein,
            int? minCarbs,
            int? maxCarbs,
            int? minFat,
            int? maxFat)
        {
            if (id == null)
            {
                return NotFound();
            }

            var (restaurant, filteredMeals) = await _restaurantService.GetRestaurantWithFilteredMealsAsync(
                id.Value,
                minCalories,
                maxCalories,
                minProtein,
                maxProtein,
                minCarbs,
                maxCarbs,
                minFat,
                maxFat);

            if (restaurant == null)
            {
                return NotFound();
            }

            ViewBag.RestaurantName = restaurant.Name;
            return PartialView("_RestaurantMealsPartial", filteredMeals);
        }

        

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUserPreferences()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (preferences, followedRestaurants) = await _userPreferenceService.GetUserPreferencesAsync(userId);

            return Json(new { preferences, followedRestaurants });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTagPreferences([FromBody] List<UserMealPreference> preferences)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _userPreferenceService.UpdateTagPreferencesAsync(userId, preferences);

            return Json(new { success = true });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollowRestaurant([FromBody] int restaurantId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (success, following) = await _userPreferenceService.ToggleFollowRestaurantAsync(userId, restaurantId);

            return Json(new { success, following });
        }
    }
}