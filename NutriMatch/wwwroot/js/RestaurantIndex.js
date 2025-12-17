let currentFilters = {
  calories: { min: 0, max: 2000 },
  protein: { min: 0, max: 150 },
  carbs: { min: 0, max: 150 },
  fats: { min: 0, max: 150 },
};

document.addEventListener("DOMContentLoaded", function () {
  initializeFilters();
  updateFilterValues();

  checkForRestaurantIdParameter();
});

function itemMatchesFilters(item) {
  return (
    item.calories >= currentFilters.calories.min &&
    item.calories <= currentFilters.calories.max &&
    item.protein >= currentFilters.protein.min &&
    item.protein <= currentFilters.protein.max &&
    item.carbs >= currentFilters.carbs.min &&
    item.carbs <= currentFilters.carbs.max &&
    item.fats >= currentFilters.fats.min &&
    item.fats <= currentFilters.fats.max
  );
}

function areFiltersDefault() {
  return (
    currentFilters.calories.min === 0 &&
    currentFilters.calories.max === 2000 &&
    currentFilters.protein.min === 0 &&
    currentFilters.protein.max === 150 &&
    currentFilters.carbs.min === 0 &&
    currentFilters.carbs.max === 150 &&
    currentFilters.fats.min === 0 &&
    currentFilters.fats.max === 150
  );
}

function updateFilterValues() {
  document.getElementById(
    "caloriesValue"
  ).textContent = `${currentFilters.calories.min} - ${currentFilters.calories.max}`;
  document.getElementById(
    "proteinValue"
  ).textContent = `${currentFilters.protein.min} - ${currentFilters.protein.max}`;
  document.getElementById(
    "carbsValue"
  ).textContent = `${currentFilters.carbs.min} - ${currentFilters.carbs.max}`;
  document.getElementById(
    "fatsValue"
  ).textContent = `${currentFilters.fats.min} - ${currentFilters.fats.max}`;
}

function applyFilters() {
  currentFilters.calories.min = parseInt(
    document.getElementById("caloriesMin").value
  );
  currentFilters.calories.max = parseInt(
    document.getElementById("caloriesMax").value
  );
  currentFilters.protein.min = parseInt(
    document.getElementById("proteinMin").value
  );
  currentFilters.protein.max = parseInt(
    document.getElementById("proteinMax").value
  );
  currentFilters.carbs.min = parseInt(
    document.getElementById("carbsMin").value
  );
  currentFilters.carbs.max = parseInt(
    document.getElementById("carbsMax").value
  );
  currentFilters.fats.min = parseInt(document.getElementById("fatsMin").value);
  currentFilters.fats.max = parseInt(document.getElementById("fatsMax").value);

  updateFilterValues();
}

function openMenu(restaurantId) {
  const f = {
    minCalories: currentFilters.calories.min,
    maxCalories: currentFilters.calories.max,
    minProtein: currentFilters.protein.min,
    maxProtein: currentFilters.protein.max,
    minCarbs: currentFilters.carbs.min,
    maxCarbs: currentFilters.carbs.max,
    minFats: currentFilters.fats.min,
    maxFats: currentFilters.fats.max,
  };

  const query = new URLSearchParams(f).toString();

  const menuContainer = document.getElementById("modal-content");
  menuContainer.innerHTML =
    '<div class="text-center p-4"><i class="fas fa-spinner fa-spin"></i> Loading menu...</div>';

  const modal = new bootstrap.Modal(document.getElementById("menuModal"));
  modal.show();

  fetch(`/Restaurants/GetRestaurantMeals/${restaurantId}?${query}`)
    .then((response) => {
      console.log("Response status:", response.status);
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return response.text();
    })
    .then((html) => {
      console.log("Received HTML length:", html.length);
      menuContainer.innerHTML = html;

      const scripts = menuContainer.querySelectorAll("script");
      scripts.forEach((script) => {
        const newScript = document.createElement("script");
        if (script.src) {
          newScript.src = script.src;
        } else {
          newScript.textContent = script.textContent;
        }
        document.body.appendChild(newScript);
        document.body.removeChild(newScript);
      });
    })
    .catch((err) => {
      console.error("Failed to fetch menu details:", err);
      menuContainer.innerHTML = `
            <div class="alert alert-danger" role="alert">
                <i class="fas fa-exclamation-triangle me-2"></i>
                Failed to load menu details. Please try again.
                <br><small>Error: ${err.message}</small>
            </div>
        `;
    });
}

function toggleItemDetails(headerElement) {
  const menuItem = headerElement.closest(".menu-item");
  const details = menuItem.querySelector(".menu-item-details");
  const chevron = headerElement.querySelector(".chevron-icon");

  const isShown = details.classList.contains("show");

  details.classList.toggle("show", !isShown);
  chevron.classList.toggle("fa-chevron-up", !isShown);
  chevron.classList.toggle("fa-chevron-down", isShown);
}

function initializeFilters() {
  const sliders = [
    "caloriesMin",
    "caloriesMax",
    "proteinMin",
    "proteinMax",
    "carbsMin",
    "carbsMax",
    "fatsMin",
    "fatsMax",
  ];

  sliders.forEach((sliderId) => {
    document.getElementById(sliderId).addEventListener("input", function () {
      const type = sliderId.replace("Min", "").replace("Max", "");
      const isMin = sliderId.includes("Min");

      const minSlider = document.getElementById(type + "Min");
      const maxSlider = document.getElementById(type + "Max");

      if (isMin && parseInt(minSlider.value) > parseInt(maxSlider.value)) {
        maxSlider.value = minSlider.value;
      } else if (
        !isMin &&
        parseInt(maxSlider.value) < parseInt(minSlider.value)
      ) {
        minSlider.value = maxSlider.value;
      }

      currentFilters[type].min = parseInt(minSlider.value);
      currentFilters[type].max = parseInt(maxSlider.value);

      updateFilterValues();
    });
  });
}

function updateSlider(type) {
  const minSlider = document.getElementById(type + "Min");
  const maxSlider = document.getElementById(type + "Max");
  const valueDisplay = document.getElementById(type + "Value");
  const fill = document.getElementById(type + "Fill");

  let minVal = parseInt(minSlider.value);
  let maxVal = parseInt(maxSlider.value);

  if (minVal > maxVal) {
    if (event.target === minSlider) {
      maxSlider.value = minVal;
      maxVal = minVal;
    } else {
      minSlider.value = maxVal;
      minVal = maxVal;
    }
  }

  valueDisplay.textContent = minVal + " - " + maxVal;

  const min = parseInt(minSlider.min);
  const max = parseInt(minSlider.max);
  const range = max - min;

  const leftPercent = ((minVal - min) / range) * 100;
  const rightPercent = ((maxVal - min) / range) * 100;

  fill.style.left = leftPercent + "%";
  fill.style.width = rightPercent - leftPercent + "%";

  applyFilters();
}

function resetFilters() {
  document.getElementById("caloriesMin").value = 0;
  document.getElementById("caloriesMax").value = 2000;
  document.getElementById("proteinMin").value = 0;
  document.getElementById("proteinMax").value = 150;
  document.getElementById("carbsMin").value = 0;
  document.getElementById("carbsMax").value = 150;
  document.getElementById("fatsMin").value = 0;
  document.getElementById("fatsMax").value = 150;

  updateSlider("calories");
  updateSlider("protein");
  updateSlider("carbs");
  updateSlider("fats");
}

let userPreferences = {
  tags: [],
  followedRestaurants: [],
};

window.addEventListener("load", function () {
  if (document.querySelector(".notification-bell")) {
    loadUserPreferences();
  }
});

async function loadUserPreferences() {
  try {
    const response = await fetch("/Restaurants/GetUserPreferences");
    if (response.ok) {
      userPreferences = await response.json();
      updateFollowButtons();
    }
  } catch (error) {
    console.error("Error loading preferences:", error);
  }
}

function openPreferencesModal() {
  const modal = new bootstrap.Modal(
    document.getElementById("preferencesModal")
  );

  userPreferences.tags.forEach((tag) => {
    const checkbox = document.getElementById(`tag-${tag}`);
    if (checkbox) {
      checkbox.checked = true;
    }
  });

  modal.show();
}

async function toggleFollow(restaurantId, event) {
  event.stopPropagation();

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;
  const btn = event.currentTarget;

  try {
    const response = await fetch("/Restaurants/ToggleFollowRestaurant", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify(restaurantId),
    });

    const data = await response.json();
    if (data.success) {
      const icon = btn.querySelector("i");

      if (data.following) {
        btn.classList.add("active");
        icon.classList.remove("far");
        icon.classList.add("fas");

        btn.classList.add("just-activated");
        setTimeout(() => btn.classList.remove("just-activated"), 500);

        showSuccess("You will be notified when this restaurant adds new meals");
        userPreferences.followedRestaurants.push(restaurantId);
      } else {
        btn.classList.remove("active");
        icon.classList.remove("fas");
        icon.classList.add("far");

        showInfo("Notifications disabled for this restaurant");
        userPreferences.followedRestaurants =
          userPreferences.followedRestaurants.filter(
            (id) => id !== restaurantId
          );
      }
    }
  } catch (error) {
    console.error("Error toggling follow:", error);
    showError("Error updating notification settings");
  }
}

function updateFollowButtons() {
  document.querySelectorAll(".notification-bell").forEach((btn) => {
    const restaurantId = parseInt(btn.dataset.restaurantId);
    if (userPreferences.followedRestaurants.includes(restaurantId)) {
      btn.classList.add("active");
      const icon = btn.querySelector("i");
      icon.classList.remove("far");
      icon.classList.add("fas");
    }
  });
}

function showSuccess(message) {
  createToast(message, "success");
}

function showError(message) {
  createToast(message, "danger");
}

function showInfo(message) {
  createToast(message, "info");
}

function createToast(message, type = "info") {
  const toastContainer =
    document.getElementById("toast-container") || createToastContainer();

  const toastId = "toast-" + Date.now();
  const toast = document.createElement("div");
  toast.id = toastId;
  toast.className = `toast align-items-center text-white bg-${type} border-0`;
  toast.setAttribute("role", "alert");

  const iconMap = {
    success: "fas fa-check-circle",
    danger: "fas fa-exclamation-circle",
    info: "fas fa-info-circle",
  };

  toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <i class="${iconMap[type]} me-2"></i>
                ${message}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" onclick="removeToast('${toastId}')"></button>
        </div>
    `;

  toastContainer.appendChild(toast);
  toast.style.display = "block";
  setTimeout(() => toast.classList.add("show"), 100);
  setTimeout(() => removeToast(toastId), 5000);
}

function removeToast(toastId) {
  const toast = document.getElementById(toastId);
  if (toast) {
    toast.classList.remove("show");
    setTimeout(() => toast.remove(), 300);
  }
}

function createToastContainer() {
  const container = document.createElement("div");
  container.id = "toast-container";
  container.className = "toast-container position-fixed top-0 end-0 p-3";
  container.style.zIndex = "10000";
  document.body.appendChild(container);
  return container;
}

function toggleTagExpansion(tagName) {
  const threshold = document.getElementById(`threshold-${tagName}`);
  const tagItem = threshold.previousElementSibling;
  const checkbox = document.getElementById(`tag-${tagName}`);

  const isExpanded = threshold.classList.contains("show");
  threshold.classList.toggle("show", !isExpanded);
  tagItem.classList.toggle("expanded", !isExpanded);

  if (!isExpanded) {
    checkbox.checked = true;
  }
}

document.addEventListener("DOMContentLoaded", function () {
  const checkboxes = document.querySelectorAll(
    '.tag-item input[type="checkbox"]'
  );
  checkboxes.forEach((checkbox) => {
    checkbox.addEventListener("click", function (e) {
      e.stopPropagation();

      const tagName = this.value;
      const threshold = document.getElementById(`threshold-${tagName}`);

      if (!this.checked && threshold) {
        threshold.classList.remove("show");
        threshold.previousElementSibling.classList.remove("expanded");
      }
    });
  });
});

function openPreferencesModal() {
  const modal = new bootstrap.Modal(
    document.getElementById("preferencesModal")
  );

  loadUserPreferencesForModal();

  modal.show();
}

async function loadUserPreferencesForModal() {
  try {
    const response = await fetch("/Restaurants/GetUserPreferences");
    if (response.ok) {
      const data = await response.json();

      document
        .querySelectorAll('.tag-selection input[type="checkbox"]')
        .forEach((cb) => {
          cb.checked = false;
        });
      document.querySelectorAll(".tag-threshold").forEach((threshold) => {
        threshold.classList.remove("show");
      });
      document.querySelectorAll(".tag-item").forEach((item) => {
        item.classList.remove("expanded");
      });

      data.preferences.forEach((pref) => {
        const checkbox = document.getElementById(`tag-${pref.tag}`);
        if (checkbox) {
          checkbox.checked = true;

          if (
            pref.thresholdValue !== null &&
            pref.thresholdValue !== undefined
          ) {
            const valueInput = document.getElementById(`value-${pref.tag}`);
            const threshold = document.getElementById(`threshold-${pref.tag}`);

            if (valueInput && threshold) {
              valueInput.value = pref.thresholdValue;
              threshold.classList.add("show");
              threshold.previousElementSibling.classList.add("expanded");
            }
          }
        }
      });
    }
  } catch (error) {
    console.error("Error loading preferences:", error);
  }
}

async function savePreferences() {
  const preferences = [];

  document
    .querySelectorAll('.tag-selection input[type="checkbox"]:checked')
    .forEach((cb) => {
      const tagValue = cb.value;
      const valueInput = document.getElementById(`value-${tagValue}`);

      const preference = {
        tag: tagValue,
        thresholdValue: valueInput ? parseInt(valueInput.value) : null,
      };

      preferences.push(preference);
    });

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  try {
    const response = await fetch("/Restaurants/UpdateTagPreferences", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify(preferences),
    });

    const data = await response.json();
    if (data.success) {
      showSuccess(
        "Preferences saved! You will receive notifications for matching meals."
      );
      bootstrap.Modal.getInstance(
        document.getElementById("preferencesModal")
      ).hide();
    } else {
      showError("Failed to save preferences");
    }
  } catch (error) {
    console.error("Error saving preferences:", error);
    showError("Error saving preferences");
  }
}

function checkForRestaurantIdParameter() {
  const urlParams = new URLSearchParams(window.location.search);
  const restaurantId = urlParams.get("restaurantId");

  if (restaurantId) {
    const newUrl = window.location.pathname;
    window.history.replaceState({}, document.title, newUrl);

    setTimeout(() => {
      openMenu(parseInt(restaurantId));
    }, 300);
  }
}
