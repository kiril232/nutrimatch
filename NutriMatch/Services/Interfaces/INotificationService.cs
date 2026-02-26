using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface INotificationService
    {
        Task<(List<Notification> notifications, int unreadCount)> GetNotificationsAsync(string userId);
        Task<List<Notification>> GetAllNotificationsAsync(string userId);
        Task<(bool success, int unreadCount)> MarkAsReadAsync(int notificationId, string userId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<(bool success, int unreadCount)> DeleteAsync(int notificationId, string userId);
        Task<(bool success, string message)> DeleteAllAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<Notification?> GetLatestNotificationAsync(string userId);
        Task SendEmailAsync(string toEmail, string subject, string message);
        Task CreateMealNotificationsAsync(RestaurantMeal meal, Restaurant restaurant);
        Task CreateRestaurantNotificationsAsync(Restaurant restaurant);
        Task CreateRecipeNotificationsAsync(Recipe recipe);
        List<string> GetMatchingTags(List<UserMealPreference> preferences, float protein, float carbs, float fat, float calories);
        Task CreateRecipeStatusNotificationAsync(string userId, string recipeTitle, int recipeId, bool isAccepted, string? declineReason = null);
        Task CreateMealPlanUpdateNotificationsAsync(List<string> affectedUserIds, string itemType);
        Task CreateRecipeRatingNotificationAsync(string recipeOwnerId, string raterUserId, string recipeTitle, int recipeId, double rating);
    }
}