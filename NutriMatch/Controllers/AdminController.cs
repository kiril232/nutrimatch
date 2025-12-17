using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriMatch.Models;
using System.Text.Json;
using NutriMatch.Services;
using NutriMatch.Data;
using Microsoft.EntityFrameworkCore;

namespace NutriMatch.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IRecipeApprovalService _recipeApprovalService;
        private readonly IRestaurantService _restaurantService;
        private readonly IMealKeywordService _mealKeywordService;
        private readonly IFileUploadService _fileUploadService;

        public AdminController(
            AppDbContext context,
            ILogger<AdminController> logger,
            IRecipeApprovalService recipeApprovalService,
            IRestaurantService restaurantService,
            IMealKeywordService mealKeywordService,
            IFileUploadService fileUploadService)
        {
            _context = context;
            _logger = logger;
            _recipeApprovalService = recipeApprovalService;
            _restaurantService = restaurantService;
            _mealKeywordService = mealKeywordService;
            _fileUploadService = fileUploadService;
        }

        public async Task<IActionResult> Index()
        {
            var pendingRecipes = await _recipeApprovalService.GetPendingRecipesAsync();
            return View(pendingRecipes);
        }

        #region Recipe Approval

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRecipe([FromBody] JsonElement request)
        {
            try
            {
                if (!request.TryGetProperty("recipeId", out var recipeIdProp))
                {
                    return Json(new { success = false, message = "Recipe ID is required." });
                }

                int recipeId = recipeIdProp.GetInt32();
                var (success, message) = await _recipeApprovalService.ApproveRecipeAsync(recipeId);

                return Json(new { message, success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving recipe");
                return Json(new { success = false, message = "An error occurred while approving the recipe." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRecipe([FromBody] JsonElement request)
        {
            try
            {
                if (!request.TryGetProperty("recipeId", out var recipeIdProp))
                {
                    return Json(new { success = false, message = "Recipe ID is required." });
                }

                int recipeId = recipeIdProp.GetInt32();
                string reason = request.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() ?? "No reason provided." : "No reason provided.";
                string notes = request.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() ?? "No notes provided." : "No notes provided.";

                var (success, message) = await _recipeApprovalService.DeclineRecipeAsync(recipeId, reason, notes);

                return Json(new { message, success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining recipe");
                return Json(new { success = false, message = "An error occurred while declining the recipe." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkApproveRecipes([FromBody] JsonElement request)
        {
            try
            {
                if (!request.TryGetProperty("recipeIds", out var recipeIdsProp))
                {
                    return Json(new { success = false, message = "Recipe IDs are required." });
                }

                List<int> recipeIds = recipeIdsProp.EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToList();

                var (success, message, approvedCount) = await _recipeApprovalService.BulkApproveRecipesAsync(recipeIds);

                return Json(new { message, success, approvedCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk approving recipes");
                return Json(new { success = false, message = "An error occurred while approving recipes." });
            }
        }

        public async Task<IActionResult> DeclineReasonModel(int? id)
        {
            try
            {
                if (id == null)
                {
                    return NotFound();
                }

                var recipe = await _recipeApprovalService.GetRecipeForDeclineAsync(id.Value);

                if (recipe == null)
                {
                    return NotFound();
                }

                return PartialView("_RecipeDeclineAdminPartial", recipe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading decline reason model");
                return StatusCode(500, "An error occurred while loading the decline form.");
            }
        }

        #endregion

        #region Ingredient Review

        [HttpGet]
        public async Task<IActionResult> GetIngredientReview(int id)
        {
            try
            {
                var ingredient = await _context.Ingredients
                .Where(i => i.Id == id && i.Status == "Pending")
                .FirstOrDefaultAsync();

                if (ingredient == null)
                {
                    return NotFound("Ingredient not found or not pending review.");
                }

                return PartialView("_IngredientReviewPartial", ingredient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ingredient review for ID: {IngredientId}", id);
                return StatusCode(500, "An error occurred while loading ingredient details.");
            }
        }

        #endregion

        #region Meal Keywords

        [HttpGet]
        public IActionResult GetMealTagsPartial()
        {
            return PartialView("_MealTagsPartial");
        }

        [HttpGet]
        public async Task<IActionResult> GetMealKeywords()
        {
            var keywords = await _mealKeywordService.GetMealKeywordsAsync();
            return Json(keywords);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMealKeyword([FromBody] MealKeyword keyword)
        {
            try
            {
                var (success, message) = await _mealKeywordService.AddMealKeywordAsync(keyword);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding keyword: " + ex.Message });
            }
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMealKeyword(int id)
        {
            try
            {
                var (success, message) = await _mealKeywordService.DeleteMealKeywordAsync(id);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting keyword: " + ex.Message });
            }
        }

        #endregion

        #region Restaurant Management

        [HttpGet]
        public IActionResult GetRestaurantMealsPartial()
        {
            return PartialView("_RestaurantMealsManagementPartial");
        }

        [HttpGet]
        public async Task<IActionResult> GetRestaurants()
        {
            var restaurants = await _restaurantService.GetRestaurantsAsync();
            return Json(restaurants);
        }

        [HttpGet]
        public async Task<IActionResult> GetRestaurant(int id)
        {
            try
            {
                var restaurant = await _restaurantService.GetRestaurantAsync(id);
                if (restaurant == null)
                {
                    return Json(new { success = false, message = "Restaurant not found" });
                }

                return Json(restaurant);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading restaurant: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRestaurantMeals(int id)
        {
            try
            {
                var meals = await _restaurantService.GetRestaurantMealsAsync(id);
                return Json(meals);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading meals: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRestaurantMeal([FromBody] RestaurantMeal meal)
        {
            try
            {
                var (success, message) = await _restaurantService.AddRestaurantMealAsync(meal);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding meal: " + ex.Message });
            }
        }

        [HttpDelete]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRestaurantMeal(int id)
        {
            try
            {
                var (success, message) = await _restaurantService.DeleteRestaurantMealAsync(id);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting meal: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRestaurantMeal([FromBody] RestaurantMeal meal)
        {
            try
            {
                var (success, message) = await _restaurantService.EditRestaurantMealAsync(meal);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating meal: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRestaurant([FromForm] string name, [FromForm] string description, [FromForm] IFormFile image)
        {
            try
            {
                var filePath = await _fileUploadService.UploadImageAsync(image);

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return Json(new { success = false, message = "Image upload failed." });
                }

                var (success, message, restaurantId) = await _restaurantService.AddRestaurantAsync(name, description, filePath);

                return Json(new { success, message, restaurantId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error adding restaurant: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRestaurant([FromForm] int id, [FromForm] string name, [FromForm] string description, [FromForm] IFormFile? image)
        {
            try
            {
                string? filePath = null;

                if (image != null && image.Length > 0)
                {
                    filePath = await _fileUploadService.UploadImageAsync(image);

                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        return Json(new { success = false, message = "Image upload failed." });
                    }
                }

                var (success, message) = await _restaurantService.EditRestaurantAsync(id, name, description, filePath);

                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating restaurant: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRestaurant(int id)
        {
            try
            {
                var (success, message) = await _restaurantService.DeleteRestaurantAsync(id);
                return Json(new { success, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting restaurant: " + ex.Message });
            }
        }

        #endregion
    }
}