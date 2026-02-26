using NutriMatch.Data;
using NutriMatch.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using Microsoft.IdentityModel.Tokens;

namespace NutriMatch.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public NotificationService(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public async Task<(List<Notification> notifications, int unreadCount)> GetNotificationsAsync(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return (notifications, unreadCount);
        }

        public async Task<List<Notification>> GetAllNotificationsAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<(bool success, int unreadCount)> MarkAsReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return (false, 0);
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return (true, unreadCount);
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(bool success, int unreadCount)> DeleteAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
            {
                return (false, 0);
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return (true, unreadCount);
        }

        public async Task<(bool success, string message)> DeleteAllAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return (false, "User not found");
                }

                var userNotifications = await _context.Notifications
                    .Where(n => n.UserId == userId)
                    .ToListAsync();

                _context.Notifications.RemoveRange(userNotifications);
                await _context.SaveChangesAsync();

                return (true, "All notifications deleted");
            }
            catch (Exception)
            {
                return (false, "Error deleting notifications");
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<Notification?> GetLatestNotificationAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var fromEmail = _config["EmailSettings:FromEmail"];
            var password = _config["EmailSettings:Password"];
            var smtpServer = _config["EmailSettings:SmtpServer"];
            var port = int.Parse(_config["EmailSettings:Port"]);

            if (!string.IsNullOrEmpty(fromEmail) && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(fromEmail) && port != 0)
            {
                using (var client = new SmtpClient(smtpServer, port))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(fromEmail, password);

                    var mailMessage = new MailMessage(fromEmail, toEmail, subject, message)
                    {
                        IsBodyHtml = true
                    };

                    await client.SendMailAsync(mailMessage);
                }
            }


        }

        public async Task CreateRecipeStatusNotificationAsync(string userId, string recipeTitle, int recipeId, bool isAccepted, string? declineReason = null)
        {
            var user = await _context.Users.FindAsync(userId);

            bool shouldNotify = isAccepted
                ? user?.NotifyRecipeAccepted == true
                : user?.NotifyRecipeDeclined == true;

            if (!shouldNotify)
            {
                return;
            }

            string notificationType;
            string message;
            string emailSubject;

            if (isAccepted)
            {
                notificationType = "RecipeAccepted";
                message = $"Great news! Your recipe '{recipeTitle}' has been approved and is now live!";
                emailSubject = "Your recipe has been approved!";
            }
            else
            {
                notificationType = "RecipeDeclined";
                message = string.IsNullOrEmpty(declineReason) || declineReason == "No reason provided."
                    ? $"Your recipe '{recipeTitle}' was declined."
                    : $"Your recipe '{recipeTitle}' was declined. Reason: {declineReason}";
                emailSubject = "Your recipe was declined";
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = notificationType,
                Message = message,
                RecipeId = recipeId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            await SendEmailAsync(
                user.Email,
                emailSubject,
                $"<p>Hi {user.UserName},</p><p>{message}</p>"
            );
        }

        public async Task CreateRestaurantNotificationsAsync(Restaurant restaurant)
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

                await SendEmailAsync(
                    user.Email,
                    "New restaurant added!",
                    $"<p>Hi {user.UserName},</p><p>New restaurant added: <b>{restaurant.Name}</b>.</p>"
                );
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateMealNotificationsAsync(RestaurantMeal meal, Restaurant restaurant)
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
                    await SendEmailAsync(
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

                    await SendEmailAsync(
                        userPref.User.Email,
                        "New meal matches your preferences",
                        $"<p>Hi {userPref.User.UserName},</p><p>{notification.Message}</p>"
                    );
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateRecipeNotificationsAsync(Recipe recipe)
        {
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
                .Where(u => u.UserId != recipe.UserId && u.User.NotifyRecipeMatchesTags)
                .ToList();

            foreach (var userPref in userPreferences)
            {
                var matchingTags = GetMatchingTags(userPref.Preferences, recipe.Protein, recipe.Carbs, recipe.Fat, recipe.Calories);

                if (matchingTags.Any())
                {
                    var tagsText = string.Join(", ", matchingTags);

                    var notification = new Notification
                    {
                        UserId = userPref.UserId,
                        Type = "RecipeMatchesTags",
                        Message = $"New recipe matches your preferences ({tagsText}): {recipe.Title}",
                        RecipeId = recipe.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);

                    await SendEmailAsync(
                        userPref.User.Email,
                        "New recipe matches your preferences",
                        $"<p>Hi {userPref.User.UserName},</p><p>{notification.Message}</p>"
                    );
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateMealPlanUpdateNotificationsAsync(List<string> affectedUserIds, string itemType)
        {
            foreach (var userId in affectedUserIds)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null || !user.NotifyMealPlanUpdated)
                {
                    continue;
                }

                var message = itemType == "recipe"
                    ? "A recipe in your meal plan was removed and has been automatically replaced with a similar recipe."
                    : "A restaurant meal in your meal plan was removed and has been automatically replaced with a similar restaurant meal.";

                var notification = new Notification
                {
                    UserId = userId,
                    Type = "MealPlanUpdated",
                    Message = message,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
        }

        public async Task CreateRecipeRatingNotificationAsync(string recipeOwnerId, string raterUserId, string recipeTitle, int recipeId, double rating)
        {
            var userNotification = await _context.Users.FindAsync(recipeOwnerId);
            if (userNotification?.NotifyRecipeRated != true)
            {
                return;
            }

            var notification = new Notification
            {
                UserId = recipeOwnerId,
                Type = "RecipeRated",
                Message = $"Your recipe '{recipeTitle}' received a new rating of {rating} stars.",
                RecipeId = recipeId,
                RelatedUserId = raterUserId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);

            await SendEmailAsync(
                userNotification.Email,
                "Your recipe was rated!",
                $"<p>Hi {userNotification.UserName},</p><p>{notification.Message}</p>"
            );
        }

        public List<string> GetMatchingTags(List<UserMealPreference> preferences, float protein, float carbs, float fat, float calories)
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

        public bool IsBalanced(float protein, float carbs, float fat, float calories)
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