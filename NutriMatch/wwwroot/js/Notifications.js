document.addEventListener("DOMContentLoaded", function () {
  const notificationPanel = document.getElementById("notification-panel");
  const notificationIcon = document.querySelector(".notification-icon");
  let isOpen = false;

  notificationIcon.addEventListener("click", function (e) {
    e.stopPropagation();

    if (isOpen) {
      closePanel();
    } else {
      openPanel();
    }
  });

  function openPanel() {
    loadNotificationPanel();
    notificationPanel.classList.add("show");
    isOpen = true;
  }

  function closePanel() {
    notificationPanel.classList.remove("show");
    isOpen = false;
  }

  document.addEventListener("click", function (e) {
    if (!e.target.closest("#notification-bell") && isOpen) {
      closePanel();
    }
  });

  function loadNotificationPanel() {
    fetch("/Notifications/NotificationPanel", {
      method: "GET",
      headers: {
        "Content-Type": "text/html",
      },
    })
      .then((response) => {
        if (!response.ok) {
          throw new Error("Network response was not ok");
        }
        return response.text();
      })
      .then((html) => {
        document.getElementById("notification-panel-content").innerHTML = html;
      })
      .catch((error) => {
        console.error("Error loading notifications:", error);
        document.getElementById("notification-panel-content").innerHTML =
          '<div class="notification-error">Failed to load notifications</div>';
      });
  }

  function loadNotificationCount() {
    fetch("/Notifications/GetNotifications", {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    })
      .then((response) => response.json())
      .then((data) => {
        updateNotificationBadge(data.unreadCount);
      })
      .catch((error) => {
        console.log("Failed to load notification count:", error);
      });
  }

  function updateNotificationBadge(count) {
    const badge = document.getElementById("notification-count");
    if (count > 0) {
      badge.textContent = count > 99 ? "99+" : count;
      badge.classList.remove("hidden");
    } else {
      badge.classList.add("hidden");
    }
  }

  document.addEventListener("click", function (e) {
    const notificationItem = e.target.closest(".notification-item");

    if (notificationItem) {
      const notifId = notificationItem.getAttribute("data-id");
      const recipeId = notificationItem.getAttribute("data-recipe-id");

      if (e.target.closest(".notification-delete-btn")) return;

      const notificationType = getNotificationType(notificationItem);

      if (!notifId) return;

      const formData = new FormData();
      formData.append("notificationId", notifId);

      fetch("/Notifications/MarkAsRead", {
        method: "POST",
        body: formData,
      })
        .then((response) => response.json())
        .then((data) => {
          if (data.success) {
            notificationItem.classList.remove("unread");
            updateNotificationBadge(data.unreadCount);

            if (recipeId && recipeId !== "" && recipeId !== "null") {
              if (notificationType === "RecipeDeclined") {
                window.location.href =
                  "/Recipes/MyRecipes?openDeclineModal=" + recipeId;
              } else if (isRestaurantNotification(notificationType)) {
                window.location.href =
                  "/Restaurants/Index?restaurantId=" + recipeId;
              } else if (isRecipeNotification(notificationType)) {
                window.location.href = "/Recipes/Index?recipeId=" + recipeId;
              }
            } else {
              if (isMealPlanNotification(notificationType)) {
                window.location.href = "/MealPlan/";
                console.log("TEST");
              }
            }
          }
        })
        .catch((error) => {
          console.error("Error marking notification as read:", error);
        });
    }
  });

  function getNotificationType(notificationItem) {
    const iconWrapper = notificationItem.querySelector(
      ".notification-icon-wrapper i"
    );
    if (!iconWrapper) return null;

    if (iconWrapper.classList.contains("fa-star")) return "RecipeRated";
    if (iconWrapper.classList.contains("fa-check-circle"))
      return "RecipeAccepted";
    if (iconWrapper.classList.contains("fa-times-circle"))
      return "RecipeDeclined";
    if (iconWrapper.classList.contains("fa-utensils"))
      return "RestaurantNewMeal";
    if (iconWrapper.classList.contains("fa-store")) return "NewRestaurant";
    if (iconWrapper.classList.contains("fa-calendar-check"))
      return "MealPlanUpdated";

    if (iconWrapper.classList.contains("fa-tags")) {
      const messageElement = notificationItem.querySelector(
        ".notification-message"
      );
      if (messageElement) {
        const message = messageElement.textContent;
        if (message.includes(" at ")) {
          return "MealMatchesTags";
        } else {
          return "RecipeMatchesTags";
        }
      }
      return "MealMatchesTags";
    }

    return null;
  }

  function isRestaurantNotification(type) {
    return ["RestaurantNewMeal", "MealMatchesTags", "NewRestaurant"].includes(
      type
    );
  }

  function isRecipeNotification(type) {
    return [
      "RecipeRated",
      "RecipeAccepted",
      "RecipeDeclined",
      "RecipeMatchesTags",
    ].includes(type);
  }

  function isMealPlanNotification(type) {
    return ["MealPlanUpdated"].includes(type);
  }

  document.addEventListener("click", function (e) {
    if (e.target.id === "mark-all-read" || e.target.closest("#mark-all-read")) {
      e.preventDefault();

      fetch("/Notifications/MarkAllAsRead", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
      })
        .then((response) => response.json())
        .then((data) => {
          if (data.success) {
            const allNotifications =
              document.querySelectorAll(".notification-item");
            allNotifications.forEach((item) => {
              item.classList.remove("unread");
            });

            updateNotificationBadge(0);

            const markAllBtn = document.getElementById("mark-all-read");
            if (markAllBtn) {
              markAllBtn.style.display = "none";
            }
          }
        })
        .catch((error) => {
          console.error("Error marking all as read:", error);
        });
    }
  });

  if (!window.notificationEventSource) {
    window.notificationEventSource = new EventSource("/Notifications/Stream");

    window.notificationEventSource.onmessage = function (event) {
      const data = JSON.parse(event.data);
      updateNotificationBadge(data.unreadCount);

      if (isOpen) {
        loadNotificationPanel();
      }
    };

    window.notificationEventSource.onerror = function (err) {
      console.warn("SSE error:", err);
    };
  }

  window.addEventListener("beforeunload", function () {
    if (window.notificationEventSource) {
      window.notificationEventSource.close();
      window.notificationEventSource = null;
    }
  });

  loadNotificationCount();

  document.addEventListener("click", function (e) {
    const deleteBtn = e.target.closest(".notification-delete-btn");

    if (deleteBtn) {
      e.stopPropagation();

      const notifId = deleteBtn.getAttribute("data-notification-id");
      const notificationItem = deleteBtn.closest(".notification-item");

      if (!notifId) return;

      const formData = new FormData();
      formData.append("notificationId", notifId);

      fetch("/Notifications/Delete", {
        method: "POST",
        body: formData,
      })
        .then((response) => response.json())
        .then((data) => {
          if (data.success) {
            notificationItem.style.opacity = "0";
            notificationItem.style.transform = "translateX(100%)";

            setTimeout(() => {
              notificationItem.remove();
              updateNotificationBadge(data.unreadCount);

              const remainingNotifications = document.querySelectorAll(
                ".notification-item:not(.no-notifications)"
              );
              if (remainingNotifications.length === 0) {
                const notificationList =
                  document.getElementById("notification-list");
                notificationList.innerHTML = `
                                <div class="notification-item no-notifications">
                                    <i class="fas fa-bell-slash"></i>
                                    <p>No notifications yet</p>
                                </div>
                            `;
              }

              const unreadNotifications = document.querySelectorAll(
                ".notification-item.unread"
              );
              if (unreadNotifications.length === 0) {
                const markAllBtn = document.getElementById("mark-all-read");
                if (markAllBtn) {
                  markAllBtn.style.display = "none";
                }
              }
            }, 300);
          }
        })
        .catch((error) => {
          console.error("Error deleting notification:", error);
        });
    }
  });
});

function deleteAllNotifications() {
  fetch("/Notifications/DeleteAll", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
  })
    .then((response) => {
      if (!response.ok) {
        throw new Error("Network response was not ok");
      }
      return response.json();
    })
    .then((data) => {
      if (data.success) {
        const notificationList = document.getElementById("notification-list");
        if (notificationList) {
          notificationList.innerHTML = `
                    <div class="notification-item no-notifications">
                        <i class="fas fa-bell-slash"></i>
                        <p>No notifications yet</p>
                    </div>
                `;
        }

        if (typeof updateNotificationBadge === "function") {
          updateNotificationBadge(0);
        }

        const deleteAllBtn = document.getElementById(
          "delete-all-notifications"
        );
        const markAllReadBtn = document.getElementById("mark-all-read");

        if (deleteAllBtn) deleteAllBtn.style.display = "none";
        if (markAllReadBtn) markAllReadBtn.style.display = "none";
      } else {
        throw new Error(data.message || "Failed to delete notifications");
      }
    })
    .catch((error) => {
      console.error("Error:", error);
      if (typeof showNotification === "function") {
        showNotification(
          "Error deleting notifications: " + error.message,
          "error"
        );
      } else {
        alert("Error deleting notifications: " + error.message);
      }
    });
}
