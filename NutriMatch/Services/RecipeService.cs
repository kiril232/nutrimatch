using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly AppDbContext _context;
        private readonly IRecipeTagService _recipeTagService;

        public RecipeService(AppDbContext context, IRecipeTagService recipeTagService)
        {
            _context = context;
            _recipeTagService = recipeTagService;
        }

        public async Task<(List<Recipe> recipes, int totalRecipes)> GetPaginatedRecipesAsync(int page, int pageSize)
        {
            var totalRecipes = await _context.Recipes
                .Where(r => r.RecipeStatus == "Accepted")
                .CountAsync();

            var recipes = await _context.Recipes
                .Where(r => r.RecipeStatus == "Accepted")
                .Include(r => r.User)
                .Include(r => r.Ratings)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            foreach (var recipe in recipes)
            {
                recipe.Rating = recipe.Ratings.Any() ? recipe.Ratings.Average(r => r.Rating) : 0;
            }

            return (recipes, totalRecipes);
        }

        public async Task<List<int>> GetUserFavoriteRecipeIdsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return new List<int>();
            }

            return await _context.FavoriteRecipes
                .Where(fr => fr.UserId == userId)
                .Select(fr => fr.RecipeId)
                .ToListAsync();
        }

        public async Task<Recipe> GetRecipeByIdAsync(int id)
        {
            return await _context.Recipes
                .Include(r => r.User)
                .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<(double averageRating, int totalRatings, double userRating, bool hasUserRated)> GetRatingDataAsync(int recipeId, string userId = null)
        {
            var ratings = await _context.RecipeRatings
                .Where(r => r.RecipeId == recipeId)
                .Select(r => new { r.Rating, r.UserId })
                .ToListAsync();

            var averageRating = ratings.Any() ? Math.Round(ratings.Average(r => r.Rating), 1) : 0;
            var totalRatings = ratings.Count;

            double userRating = 0;
            bool hasUserRated = false;

            if (!string.IsNullOrEmpty(userId))
            {
                var userRatingData = ratings.FirstOrDefault(r => r.UserId == userId);
                if (userRatingData != null)
                {
                    userRating = userRatingData.Rating;
                    hasUserRated = true;
                }
            }

            return (averageRating, totalRatings, userRating, hasUserRated);
        }

        public async Task<bool> IsRecipeFavoritedAsync(string userId, int recipeId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            return await _context.FavoriteRecipes
                .AnyAsync(fr => fr.UserId == userId && fr.RecipeId == recipeId);
        }

        public async Task<List<Recipe>> GetUserRecipesAsync(string userId)
        {
            var userRecipes = await _context.Recipes
                .Where(r => r.UserId == userId)
                .Include(r => r.User)
                .Include(r => r.Ratings)
                .ToListAsync();

            foreach (var recipe in userRecipes)
            {
                recipe.Rating = recipe.Ratings.Any() ? recipe.Ratings.Average(r => r.Rating) : 0;
            }

            return userRecipes;
        }

        public async Task<double> GetUserAverageRatingAsync(string userId)
        {
            var userRecipes = await _context.Recipes
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync();

            var ratings = await _context.RecipeRatings
                .Where(r => userRecipes.Contains(r.RecipeId))
                .GroupBy(r => r.RecipeId)
                .Select(g => g.Average(r => r.Rating))
                .ToListAsync();

            if (ratings.Any())
            {
                return Math.Round(ratings.Average(), 1);
            }

            return 0;
        }

        public async Task<(Recipe recipe, float totalCalories, float totalProtein, float totalCarbs, float totalFat, bool hasPendingIngredients)> CalculateRecipeNutritionAsync(Recipe recipe, List<SelectedIngredient> ingredients)
        {
            float totalCalories = 0;
            float totalProtein = 0;
            float totalCarbs = 0;
            float totalFat = 0;
            bool hasPendingIngredients = false;

            foreach (var i in ingredients)
            {
                var tempIngredient = await _context.Ingredients.FindAsync(i.Id);

                if (tempIngredient != null)
                {
                    totalCalories += ConvertUnit(tempIngredient.Calories, i.Unit) * i.Quantity;
                    totalProtein += ConvertUnit(tempIngredient.Protein, i.Unit) * i.Quantity;
                    totalCarbs += ConvertUnit(tempIngredient.Carbs, i.Unit) * i.Quantity;
                    totalFat += ConvertUnit(tempIngredient.Fat, i.Unit) * i.Quantity;

                    if (tempIngredient.Status == "Pending")
                    {
                        hasPendingIngredients = true;
                    }
                }
            }

            return (recipe, totalCalories, totalProtein, totalCarbs, totalFat, hasPendingIngredients);
        }

        public async Task<Recipe> CreateRecipeAsync(Recipe recipe, List<SelectedIngredient> ingredients, string imageUrl)
        {
            recipe.ImageUrl = imageUrl;
            recipe.Type = new List<string> { " " };
            
            _context.Add(recipe);
            await _context.SaveChangesAsync();

            var (_, totalCalories, totalProtein, totalCarbs, totalFat, hasPendingIngredients) = 
                await CalculateRecipeNutritionAsync(recipe, ingredients);

            foreach (var i in ingredients)
            {
                _context.RecipeIngredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = i.Id,
                    Unit = i.Unit,
                    Quantity = i.Quantity
                });
            }

            recipe.Calories = MathF.Round(totalCalories, MidpointRounding.AwayFromZero);
            recipe.Protein = MathF.Round(totalProtein, MidpointRounding.AwayFromZero);
            recipe.Carbs = MathF.Round(totalCarbs, MidpointRounding.AwayFromZero);
            recipe.Fat = MathF.Round(totalFat, MidpointRounding.AwayFromZero);
            recipe.HasPendingIngredients = hasPendingIngredients;
            recipe.Type = _recipeTagService.GenerateRecipeTags(recipe, ingredients);

            _context.Update(recipe);
            await _context.SaveChangesAsync();

            return recipe;
        }

        public async Task<Recipe> UpdateRecipeAsync(Recipe recipe, List<SelectedIngredient> ingredients, string imageUrl)
        {
            recipe.ImageUrl = imageUrl;

            await _context.RecipeIngredients
                .Where(ri => ri.RecipeId == recipe.Id)
                .ExecuteDeleteAsync();

            var (_, totalCalories, totalProtein, totalCarbs, totalFat, _) = 
                await CalculateRecipeNutritionAsync(recipe, ingredients);

            foreach (var i in ingredients)
            {
                _context.RecipeIngredients.Add(new RecipeIngredient
                {
                    RecipeId = recipe.Id,
                    IngredientId = i.Id,
                    Unit = i.Unit,
                    Quantity = i.Quantity
                });
            }

            recipe.Calories = MathF.Round(totalCalories, MidpointRounding.AwayFromZero);
            recipe.Protein = MathF.Round(totalProtein, MidpointRounding.AwayFromZero);
            recipe.Carbs = MathF.Round(totalCarbs, MidpointRounding.AwayFromZero);
            recipe.Fat = MathF.Round(totalFat, MidpointRounding.AwayFromZero);
            recipe.Type = _recipeTagService.GenerateRecipeTags(recipe, ingredients);

            _context.Update(recipe);
            await _context.SaveChangesAsync();

            return recipe;
        }

        public async Task DeleteRecipeAsync(int recipeId)
        {
            var recipe = await _context.Recipes.FindAsync(recipeId);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
        }

        public float ConvertUnit(float number, string unit)
        {
            float result;
            switch (unit.ToLower())
            {
                case "g":
                case "ml":
                    result = number / 100;
                    break;
                case "oz":
                    result = (float)(number * 28.3495 / 100);
                    break;
                case "tbsp":
                    result = (float)(number * 15 / 100);
                    break;
                case "tsp":
                    result = (float)(number * 5 / 100);
                    break;
                case "cup":
                    result = (float)(number * 240 / 100);
                    break;
                default:
                    return 0;
            }

            return result;
        }

        public async Task<(bool success, string message, bool isFavorited)> ToggleFavoriteAsync(string userId, int recipeId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "User not authenticated", false);
            }

            var recipe = await _context.Recipes.FindAsync(recipeId);
            if (recipe == null)
            {
                return (false, "Recipe not found", false);
            }

            var existingFavorite = await _context.FavoriteRecipes
                .FirstOrDefaultAsync(fr => fr.UserId == userId && fr.RecipeId == recipeId);

            bool isFavorited;
            string message;

            if (existingFavorite != null)
            {
                _context.FavoriteRecipes.Remove(existingFavorite);
                isFavorited = false;
                message = "Removed from favorites";
            }
            else
            {
                var favoriteRecipe = new FavoriteRecipe
                {
                    UserId = userId,
                    RecipeId = recipeId
                };
                _context.FavoriteRecipes.Add(favoriteRecipe);
                isFavorited = true;
                message = "Added to favorites";
            }

            await _context.SaveChangesAsync();

            return (true, message, isFavorited);
        }
    }
}