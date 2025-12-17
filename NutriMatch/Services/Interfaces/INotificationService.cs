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
    }
}