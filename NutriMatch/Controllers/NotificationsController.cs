using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NutriMatch.Models;
using NutriMatch.Services;

namespace NutriMatch.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<JsonResult> GetNotifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Json(new { notifications = new List<Notification>(), unreadCount = 0 });

            var (notifications, unreadCount) = await _notificationService.GetNotificationsAsync(userId);

            return Json(new
            {
                notifications = notifications,
                unreadCount = unreadCount
            });
        }

        public async Task<ActionResult> NotificationPanel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return PartialView("_NotificationPanel", new List<Notification>());

            var notifications = await _notificationService.GetAllNotificationsAsync(userId);

            return PartialView("_NotificationPanel", notifications);
        }

        [HttpPost]
        public async Task<JsonResult> MarkAsRead(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Json(new { success = false });

            var (success, unreadCount) = await _notificationService.MarkAsReadAsync(notificationId, userId);

            return Json(new { success = success, unreadCount = unreadCount });
        }

        [HttpPost]
        public async Task<JsonResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Json(new { success = false });

            var success = await _notificationService.MarkAllAsReadAsync(userId);

            return Json(new { success = success, unreadCount = 0 });
        }

        [HttpPost]
        public async Task<JsonResult> Delete(int notificationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Json(new { success = false });

            var (success, unreadCount) = await _notificationService.DeleteAsync(notificationId, userId);

            return Json(new { success = success, unreadCount = unreadCount });
        }

        [HttpPost]
        public async Task<JsonResult> DeleteAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var (success, message) = await _notificationService.DeleteAllAsync(userId);

            return Json(new { success = success, message = message });
        }

        [HttpGet("/Notifications/Stream")]
        public async Task Stream()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return;

            var lastUnreadCount = await _notificationService.GetUnreadCountAsync(userId);

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

                if (unreadCount != lastUnreadCount)
                {
                    lastUnreadCount = unreadCount;

                    var newNotification = await _notificationService.GetLatestNotificationAsync(userId);

                    var payload = new
                    {
                        unreadCount,
                        latestMessage = newNotification?.Message,
                        createdAt = newNotification?.CreatedAt
                    };

                    var json = System.Text.Json.JsonSerializer.Serialize(payload);
                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();
                }

                await Task.Delay(3000);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
    }
}