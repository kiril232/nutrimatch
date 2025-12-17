using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text.Json;
using NutriMatch.Models;
using NutriMatch.Services;

namespace NutriMatch.Controllers
{
    public class MealKeywords
    {
        public List<string> Breakfast { get; set; }
        public List<string> Main { get; set; }
        public List<string> Snack { get; set; }
    }

    public class RecipesController : Controller
    {
        private readonly IMealPlanService _mealPlanService;
        private readonly IRecipeService _recipeService;
        private readonly IRatingService _ratingService;
        private readonly IIngredientService _ingredientService;
        private readonly IFileUploadService _fileUploadService;

        public RecipesController(
            IMealPlanService mealPlanService,
            IRecipeService recipeService,
            IRatingService ratingService,
            IIngredientService ingredientService,
            IFileUploadService fileUploadService)
        {
            _mealPlanService = mealPlanService;
            _recipeService = recipeService;
            _ratingService = ratingService;
            _ingredientService = ingredientService;
            _fileUploadService = fileUploadService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 6)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var (recipes, totalRecipes) = await _recipeService.GetPaginatedRecipesAsync(page, pageSize);
            var favoriteRecipeIds = await _recipeService.GetUserFavoriteRecipeIdsAsync(userId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var response = new
                {
                    recipes = recipes.Select(recipe => new
                    {
                        Id = recipe.Id,
                        Title = recipe.Title,
                        ImageUrl = recipe.ImageUrl,
                        Rating = recipe.Rating,
                        UserName = recipe.User.UserName,
                        CreatedAt = recipe.CreatedAt.ToString("MMM dd, yyyy"),
                        Calories = recipe.Calories,
                        Protein = recipe.Protein,
                        Carbs = recipe.Carbs,
                        Fat = recipe.Fat,
                        IsOwner = recipe.User.Id == userId,
                        IsFavorited = favoriteRecipeIds.Contains(recipe.Id)
                    }),
                    hasMorePages = (page * pageSize) < totalRecipes,
                    currentPage = page,
                    totalRecipes = totalRecipes
                };

                return Json(response);
            }

            ViewBag.FavoriteRecipeIds = favoriteRecipeIds;
            ViewBag.userId = userId;
            ViewBag.HasMorePages = (page * pageSize) < totalRecipes;
            ViewBag.CurrentPage = page;
            ViewBag.TotalRecipes = totalRecipes;

            return View(recipes);
        }

        [Route("Recipes/Details/{id}")]
        public async Task<IActionResult> Details(int? id, bool isOwner = false, string recipeDetailsDisplayContorol = "")
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _recipeService.GetRecipeByIdAsync(id.Value);

            if (recipe == null)
            {
                return NotFound();
            }

            if (recipeDetailsDisplayContorol == "Declined")
            {
                return PartialView("_RecipeDeclinePartial", recipe);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool actualIsOwner = !string.IsNullOrEmpty(userId) && recipe.UserId == userId;

            var (averageRating, totalRatings, userRating, hasUserRated) =
                await _recipeService.GetRatingDataAsync(id.Value, userId);

            bool isFavorited = await _recipeService.IsRecipeFavoritedAsync(userId, id.Value);

            if (recipeDetailsDisplayContorol == "Buttons")
            {
                ViewBag.AddAdminButtons = true;
            }
            else if (recipeDetailsDisplayContorol == "Index")
            {
                ViewBag.InIndex = true;
            }

            ViewBag.IsOwner = actualIsOwner;
            ViewBag.AverageRating = averageRating;
            ViewBag.TotalRatings = totalRatings;
            ViewBag.UserRating = userRating;
            ViewBag.HasUserRated = hasUserRated;
            ViewBag.IsFavorited = isFavorited;

            return PartialView("_RecipeDetailsPartial", recipe);
        }

        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Instructions")] Recipe recipe)
        {
            if (!ModelState.IsValid)
            {
                return View(recipe);
            }

            var file = Request.Form.Files.GetFile("RecipeImage");
            var imageUrl = await _fileUploadService.UploadImageAsync(file);

            recipe.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            string selectedIngredients = Request.Form["Ingredients"];
            List<SelectedIngredient> ingredients = JsonSerializer.Deserialize<List<SelectedIngredient>>(selectedIngredients);

            await _recipeService.CreateRecipeAsync(recipe, ingredients, imageUrl);

            return RedirectToAction("MyRecipes");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id, bool requiresChange = false)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _recipeService.GetRecipeByIdAsync(id.Value);

            if (recipe == null)
            {
                return NotFound();
            }

            if (recipe.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
            {
                return Forbid();
            }

            ViewBag.RequireChange = requiresChange;

            return View(recipe);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,Title,Instructions")] Recipe recipe)
        {
            if (!ModelState.IsValid)
            {
                return View(recipe);
            }

            var file = Request.Form.Files.GetFile("RecipeImage");
            string imageUrl;

            if (file != null && file.Length > 0)
            {
                imageUrl = await _fileUploadService.UploadImageAsync(file);
            }
            else
            {
                imageUrl = Request.Form["ExistingImageUrl"].ToString();
            }

            recipe.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            string selectedIngredients = Request.Form["Ingredients"];
            List<SelectedIngredient> ingredients = JsonSerializer.Deserialize<List<SelectedIngredient>>(selectedIngredients);

            await _recipeService.UpdateRecipeAsync(recipe, ingredients, imageUrl);

            return RedirectToAction(nameof(MyRecipes));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var recipe = await _recipeService.GetRecipeByIdAsync(id.Value);

            if (recipe == null)
            {
                return NotFound();
            }

            return PartialView("_RecipeDeletePartial", recipe);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recipe = await _recipeService.GetRecipeByIdAsync(id);

            if (recipe != null)
            {
                await _fileUploadService.DeleteImageAsync(recipe.ImageUrl);
                await _mealPlanService.HandleDeletedRecipeAsync(id);
                await _recipeService.DeleteRecipeAsync(id);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<ActionResult<List<Ingredient>>> getSuggestions([FromQuery] string query)
        {
            var suggestions = await _ingredientService.GetIngredientSuggestionsAsync(query);
            return suggestions;
        }

        public async Task<ActionResult> MyRecipes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRecipes = await _recipeService.GetUserRecipesAsync(userId);
            var averageRating = await _recipeService.GetUserAverageRatingAsync(userId);

            ViewBag.AverageRating = averageRating;

            return View(userRecipes);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Rate([FromBody] JsonElement body)
        {
            int recipeId = body.GetProperty("recipeId").GetInt32();
            double rating = body.GetProperty("rating").GetDouble();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var (success, message, averageRating, totalRatings) = 
                await _ratingService.AddOrUpdateRatingAsync(userId, recipeId, rating);

            if (!success)
            {
                return Json(new { success = false, message });
            }

            return Json(new
            {
                success = true,
                averageRating,
                totalRatings,
                message
            });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRating([FromBody] JsonElement body)
        {
            int recipeId = body.GetProperty("recipeId").GetInt32();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var (success, message, averageRating, totalRatings) = 
                await _ratingService.RemoveRatingAsync(userId, recipeId);

            if (!success)
            {
                return Json(new { success = false, message });
            }

            return Json(new
            {
                success = true,
                averageRating,
                totalRatings,
                message
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorite([FromBody] JsonElement request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int recipeId = request.GetProperty("recipeId").GetInt32();

            var (success, message, isFavorited) = 
                await _recipeService.ToggleFavoriteAsync(userId, recipeId);

            if (!success)
            {
                return Json(new { success = false, message });
            }

            return Json(new
            {
                success = true,
                isFavorited,
                message
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddIngredient([FromBody] JsonElement request)
        {
            string name = request.GetProperty("Name").GetString();
            float calories = request.GetProperty("Calories").GetSingle();
            float protein = request.GetProperty("Protein").GetSingle();
            float carbs = request.GetProperty("Carbs").GetSingle();
            float fat = request.GetProperty("Fat").GetSingle();

            var token = Request.Headers["RequestVerificationToken"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("Anti-forgery token missing.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, ingredient) = 
                await _ingredientService.AddIngredientAsync(name, calories, protein, carbs, fat);

            if (!success)
            {
                return BadRequest(message);
            }

            return Json(new
            {
                id = ingredient.Id,
                name = ingredient.Name,
                calories = ingredient.Calories,
                protein = ingredient.Protein,
                carbs = ingredient.Carbs,
                fat = ingredient.Fat,
                success = true
            });
        }
    }
}