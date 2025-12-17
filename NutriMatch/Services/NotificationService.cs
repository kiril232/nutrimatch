
using NutriMatch.Data;
using NutriMatch.Models;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;

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
}