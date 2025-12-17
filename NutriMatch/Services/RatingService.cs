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

            // Send notification
            var userNotification = await _context.Users.FindAsync(recipe.UserId);
            if (userNotification?.NotifyRecipeRated == true)
            {
                var notification = new Notification
                {
                    UserId = recipe.UserId,
                    Type = "RecipeRated",
                    Message = $"Your recipe '{recipe.Title}' received a new rating of {rating} stars.",
                    RecipeId = recipe.Id,
                    RelatedUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);

                await _notificationService.SendEmailAsync(
                    userNotification.Email,
                    "Your recipe was rated!",
                    $"<p>Hi {userNotification.UserName},</p><p>{notification.Message}</p>"
                );
            }

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