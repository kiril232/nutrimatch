using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class RatingService : IRatingService
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public RatingService(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<(bool success, string message, double averageRating, int totalRatings)> AddOrUpdateRatingAsync(string userId, int recipeId, double rating)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "User not authenticated", 0, 0);
            }

            if (rating < 1 || rating > 5)
            {
                return (false, "Rating must be between 1 and 5", 0, 0);
            }

            var recipe = await _context.Recipes.FindAsync(recipeId);
            if (recipe == null)
            {
                return (false, "Recipe not found", 0, 0);
            }

            var existingRating = await _context.RecipeRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.RecipeId == recipeId);

            if (existingRating != null)
            {
                existingRating.Rating = rating;
                _context.RecipeRatings.Update(existingRating);
            }
            else
            {
                var newRating = new RecipeRating
                {
                    UserId = userId,
                    RecipeId = recipeId,
                    Rating = rating
                };
                _context.RecipeRatings.Add(newRating);
            }

            await _notificationService.CreateRecipeRatingNotificationAsync(
                recipe.UserId,
                userId,
                recipe.Title,
                recipe.Id,
                rating
            );

            await _context.SaveChangesAsync();

            var ratings = await _context.RecipeRatings
                .Where(r => r.RecipeId == recipeId)
                .Select(r => r.Rating)
                .ToListAsync();

            var averageRating = ratings.Any() ? Math.Round(ratings.Average(), 1) : 0;
            var totalRatings = ratings.Count;

            return (true, "Rating submitted successfully", averageRating, totalRatings);
        }

        public async Task<(bool success, string message, double averageRating, int totalRatings)> RemoveRatingAsync(string userId, int recipeId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "User not authenticated", 0, 0);
            }

            var existingRating = await _context.RecipeRatings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.RecipeId == recipeId);

            if (existingRating == null)
            {
                return (false, "No rating found to remove", 0, 0);
            }

            _context.RecipeRatings.Remove(existingRating);
            await _context.SaveChangesAsync();

            var ratings = await _context.RecipeRatings
                .Where(r => r.RecipeId == recipeId)
                .Select(r => r.Rating)
                .ToListAsync();

            var averageRating = ratings.Any() ? Math.Round(ratings.Average(), 1) : 0;
            var totalRatings = ratings.Count;

            return (true, "Rating removed successfully", averageRating, totalRatings);
        }
    }


}