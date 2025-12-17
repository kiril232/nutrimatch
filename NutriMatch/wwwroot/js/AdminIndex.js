document.addEventListener("DOMContentLoaded", function () {
  setupBulkActions();
  setupSearchFunctionality();
  setupSortingFunctionality();
  updateDisplayCount();
});

function setupBulkActions() {
  const selectAllCheckbox = document.getElementById("selectAll");
  const recipeCheckboxes = document.querySelectorAll(".recipe-checkbox");
  const bulkApproveBtn = document.getElementById("bulkApprove");

  selectAllCheckbox.addEventListener("change", function () {
    recipeCheckboxes.forEach((checkbox) => {
      checkbox.checked = this.checked;
      toggleRecipeSelection(checkbox);
    });
    updateBulkActionButtons();
  });

  recipeCheckboxes.forEach((checkbox) => {
    checkbox.addEventListener("change", function () {
      toggleRecipeSelection(this);
      updateBulkActionButtons();
      updateSelectAllState();
    });
  });

  bulkApproveBtn.addEventListener("click", handleBulkApprove);
}

function toggleRecipeSelection(checkbox) {
  const recipeCard = checkbox.closest(".recipe-card");
  if (checkbox.checked) {
    recipeCard.classList.add("selected");
  } else {
    recipeCard.classList.remove("selected");
  }
}

function updateBulkActionButtons() {
  const selectedCheckboxes = document.querySelectorAll(
    ".recipe-checkbox:checked"
  );
  const bulkApproveBtn = document.getElementById("bulkApprove");
  const bulkActionsSection = document.getElementById("bulkActionsSection");

  const hasSelections = selectedCheckboxes.length > 0;
  bulkApproveBtn.disabled = !hasSelections;

  if (hasSelections) {
    bulkActionsSection.classList.add("show");
  } else {
    bulkActionsSection.classList.remove("show");
  }
}

function updateSelectAllState() {
  const selectAllCheckbox = document.getElementById("selectAll");
  const recipeCheckboxes = document.querySelectorAll(".recipe-checkbox");
  const checkedBoxes = document.querySelectorAll(".recipe-checkbox:checked");

  if (checkedBoxes.length === 0) {
    selectAllCheckbox.indeterminate = false;
    selectAllCheckbox.checked = false;
  } else if (checkedBoxes.length === recipeCheckboxes.length) {
    selectAllCheckbox.indeterminate = false;
    selectAllCheckbox.checked = true;
  } else {
    selectAllCheckbox.indeterminate = true;
  }
}

function approveRecipe(recipeId) {
  showLoadingOverlay();

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch("/Admin/ApproveRecipe", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
    body: JSON.stringify({ recipeId: recipeId }),
  })
    .then((response) => response.json())
    .then((data) => {
      hideLoadingOverlay();
      if (data.success) {
        showSuccess("Recipe approved successfully!");
        removeRecipeCard(recipeId);
        hideRecipeDetails();
      } else {
        showError(data.message || "Failed to approve recipe");
      }
    })
    .catch((error) => {
      hideLoadingOverlay();
      console.error("Error:", error);
      showError("An error occurred while approving the recipe");
    });
}

function declineRecipe(recipeId) {
  fetch(`/Admin/DeclineReasonModel/${recipeId}`)
    .then((response) => {
      if (!response.ok) {
        throw new Error("Failed to load decline modal");
      }
      return response.text();
    })
    .then((html) => {
      const modalContainer = document.getElementById("declineModalContainer");
      modalContainer.innerHTML = html;

      const scripts = modalContainer.querySelectorAll("script");
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

      hideRecipeDetails();

      const modalElement = modalContainer.querySelector("#recipeDeclineModal");
      if (modalElement) {
        const modal = new bootstrap.Modal(modalElement);
        modal.show();
      }
    })
    .catch((error) => {
      console.error("Error loading decline modal:", error);
      showError("Failed to load decline modal");
    });
}

function cancelDecline(recipeId) {
  const declineSection = document.getElementById(`declineReason_${recipeId}`);
  const recipeCard = declineSection.closest(".recipe-card");

  declineSection.style.display = "none";

  const actionButtons = recipeCard.querySelectorAll(
    ".admin-actions-buttons .btn"
  );
  actionButtons.forEach((btn) => {
    btn.style.display = "";
  });

  document.getElementById(`declineSelect_${recipeId}`).value = "";
  document.getElementById(`declineNotes_${recipeId}`).value = "";
}

function handleBulkApprove() {
  const selectedRecipes = getSelectedRecipeIds();

  if (selectedRecipes.length === 0) {
    showError("No recipes selected");
    return;
  }

  showLoadingOverlay();

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch("/Admin/BulkApproveRecipes", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
    body: JSON.stringify({ recipeIds: selectedRecipes }),
  })
    .then((response) => response.json())
    .then((data) => {
      hideLoadingOverlay();

      if (data.success) {
        showSuccess(`${data.approvedCount} recipe(s) approved successfully!`);
        selectedRecipes.forEach((recipeId) => removeRecipeCard(recipeId));
      } else {
        showError(data.message || "Failed to approve recipes");
      }
    })
    .catch((error) => {
      hideLoadingOverlay();
      console.error("Error:", error);
      showError("An error occurred during bulk approval");
    });
}

function getSelectedRecipeIds() {
  const selectedCheckboxes = document.querySelectorAll(
    ".recipe-checkbox:checked"
  );
  return Array.from(selectedCheckboxes).map((checkbox) =>
    parseInt(checkbox.dataset.recipeId)
  );
}

function setupSearchFunctionality() {
  const searchInput = document.getElementById("searchInput");
  let searchTimeout;

  searchInput.addEventListener("input", function () {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      filterRecipes();
    }, 300);
  });
}

function filterRecipes() {
  const searchTerm = document.getElementById("searchInput").value.toLowerCase();
  const recipeCards = document.querySelectorAll(".recipe-card");
  let visibleCount = 0;

  recipeCards.forEach((card) => {
    const title = card.querySelector(".recipe-title").textContent.toLowerCase();
    const author = card
      .querySelector(".recipe-meta span")
      .textContent.toLowerCase();

    const isVisible = title.includes(searchTerm) || author.includes(searchTerm);
    card.style.display = isVisible ? "block" : "none";

    if (isVisible) visibleCount++;
  });

  updateDisplayCount(visibleCount);
}

function setupSortingFunctionality() {
  const sortFilter = document.getElementById("sortFilter");
  sortFilter.addEventListener("change", function () {
    sortRecipes(this.value);
  });
}

function sortRecipes(sortBy) {
  const recipeGrid = document.getElementById("recipeGrid");
  const recipeCards = Array.from(recipeGrid.querySelectorAll(".recipe-card"));

  recipeCards.sort((a, b) => {
    switch (sortBy) {
      case "newest":
        return parseInt(b.dataset.recipeId) - parseInt(a.dataset.recipeId);
      case "oldest":
        return parseInt(a.dataset.recipeId) - parseInt(b.dataset.recipeId);
      case "author":
        const authorA = a
          .querySelector(".recipe-meta span")
          .textContent.toLowerCase();
        const authorB = b
          .querySelector(".recipe-meta span")
          .textContent.toLowerCase();
        return authorA.localeCompare(authorB);
      case "calories":
        return parseInt(a.dataset.calories) - parseInt(b.dataset.calories);
      default:
        return 0;
    }
  });

  recipeCards.forEach((card) => recipeGrid.appendChild(card));
}

function showRecipeDetails(recipeId, isAdmin = false, recipeControl = "") {
  currentRecipeId = recipeId;
  const params = new URLSearchParams({
    isOwner: true,
    recipeDetailsDisplayContorol: recipeControl,
  });

  fetch(`/Recipes/Details/${recipeId}?${params}`)
    .then((response) => {
      if (!response.ok) {
        throw new Error("Network response was not ok");
      }
      return response.text();
    })
    .then((html) => {
      const modalContainer = document.getElementById("modalWindow");
      modalContainer.innerHTML = html;

      const existingScripts = document.querySelectorAll(
        "script[data-recipe-modal]"
      );
      existingScripts.forEach((script) => script.remove());

      const scripts = modalContainer.querySelectorAll("script");
      scripts.forEach((script) => {
        const newScript = document.createElement("script");
        newScript.setAttribute("data-recipe-modal", "true");

        if (script.src) {
          newScript.src = script.src;
        } else {
          newScript.textContent = `
            (function() {
                ${script.textContent}
            })();
        `;
        }
        document.body.appendChild(newScript);
        document.body.removeChild(newScript);
      });

      const modalElement = modalContainer.querySelector(".modal");
      if (modalElement) {
        const modal = new bootstrap.Modal(modalElement);
        modal.show();

        modalElement.addEventListener("hidden.bs.modal", function () {
          modalContainer.innerHTML = "";
          if (typeof clickedCard !== "undefined") {
            clickedCard.classList.remove("loading");
          }
        });

        modalElement.addEventListener("shown.bs.modal", function () {
          if (typeof clickedCard !== "undefined") {
            clickedCard.classList.remove("loading");
          }
        });
      } else {
        if (typeof clickedCard !== "undefined") {
          clickedCard.classList.remove("loading");
        }
      }
    })
    .catch((err) => {
      console.error("Failed to fetch recipe details", err);
      showError("Failed to load recipe details. Please try again.");
      if (typeof clickedCard !== "undefined") {
        clickedCard.classList.remove("loading");
      }
    });
}

function removeRecipeCard(recipeId) {
  const recipeCard = document.querySelector(`[data-recipe-id="${recipeId}"]`);
  if (recipeCard) {
    recipeCard.classList.add("removing");
    setTimeout(() => {
      recipeCard.remove();
      updateDisplayCount();
      updateBulkActionButtons();
      updateSelectAllState();

      if (document.querySelectorAll(".recipe-card").length === 0) {
        location.reload();
      }
    }, 500);
  }
}

function updateDisplayCount(count = null) {
  const displayCountElement = document.getElementById("displayCount");
  if (count === null) {
    count = document.querySelectorAll(
      '.recipe-card:not([style*="display: none"])'
    ).length;
  }
  displayCountElement.textContent = count;
}

function showLoadingOverlay() {
  document.getElementById("loadingOverlay").style.display = "flex";
}

function hideLoadingOverlay() {
  document.getElementById("loadingOverlay").style.display = "none";
}

function createToast(message, type = "info") {
  const toastContainer =
    document.getElementById("toast-container") || createToastContainer();

  const toastId = "toast-" + Date.now();
  const toast = document.createElement("div");
  toast.id = toastId;
  toast.className = `toast align-items-center text-white bg-${type} border-0`;
  toast.setAttribute("role", "alert");
  toast.setAttribute("aria-live", "assertive");
  toast.setAttribute("aria-atomic", "true");

  const iconMap = {
    success: "fas fa-check-circle",
    danger: "fas fa-exclamation-circle",
    warning: "fas fa-exclamation-triangle",
    info: "fas fa-info-circle",
  };

  toast.innerHTML = `
    <div class="d-flex">
        <div class="toast-body">
            <i class="${iconMap[type] || iconMap.info} me-2"></i>
            ${message}
        </div>
        <button type="button" class="btn-close btn-close-white me-2 m-auto" onclick="removeToast('${toastId}')"></button>
    </div>
`;

  toastContainer.appendChild(toast);

  toast.style.display = "block";
  setTimeout(() => {
    toast.classList.add("show");
  }, 100);

  setTimeout(() => {
    removeToast(toastId);
  }, 5000);
}

function removeToast(toastId) {
  const toast = document.getElementById(toastId);
  if (toast) {
    toast.classList.remove("show");
    setTimeout(() => {
      if (toast.parentNode) {
        toast.remove();
      }
    }, 300);
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

function showSuccess(message) {
  createToast(message, "success");
}

function showError(message) {
  createToast(message, "danger");
}

function showWarning(message) {
  createToast(message, "warning");
}

function showInfo(message) {
  createToast(message, "info");
}

function showConfirmation(message, onConfirm, onCancel = null) {
  const toastContainer =
    document.getElementById("toast-container") || createToastContainer();

  const toastId = "toast-confirm-" + Date.now();
  const toast = document.createElement("div");
  toast.id = toastId;
  toast.className = "toast align-items-center text-white bg-warning border-0";
  toast.setAttribute("role", "alert");
  toast.setAttribute("aria-live", "assertive");
  toast.setAttribute("aria-atomic", "true");
  toast.style.minWidth = "350px";

  toast.innerHTML = `
    <div class="d-flex flex-column p-2">
        <div class="toast-body mb-2">
            <i class="fas fa-question-circle me-2"></i>
            ${message}
        </div>
        <div class="d-flex gap-2 px-2 pb-2">
            <button type="button" class="btn btn-sm btn-light flex-grow-1" onclick="confirmAction('${toastId}', true)">
                <i class="fas fa-check me-1"></i>Yes
            </button>
            <button type="button" class="btn btn-sm btn-secondary flex-grow-1" onclick="confirmAction('${toastId}', false)">
                <i class="fas fa-times me-1"></i>No
            </button>
        </div>
    </div>
  `;

  toastContainer.appendChild(toast);

  toast.style.display = "block";
  setTimeout(() => {
    toast.classList.add("show");
  }, 100);

  window[`confirmCallback_${toastId}`] = { onConfirm, onCancel };

  setTimeout(() => {
    removeToast(toastId);
    if (window[`confirmCallback_${toastId}`]) {
      delete window[`confirmCallback_${toastId}`];
    }
  }, 10000);
}

function confirmAction(toastId, confirmed) {
  const callbacks = window[`confirmCallback_${toastId}`];

  if (callbacks) {
    if (confirmed && callbacks.onConfirm) {
      callbacks.onConfirm();
    } else if (!confirmed && callbacks.onCancel) {
      callbacks.onCancel();
    }
    delete window[`confirmCallback_${toastId}`];
  }

  removeToast(toastId);
}

function refreshPendingRecipes() {
  showLoadingOverlay();
  location.reload();
}

document.addEventListener("keydown", function (e) {
  if (e.key === "Escape") {
    closeModal();
  }
});

console.log("Admin Panel initialized successfully");

function hideRecipeDetails() {
  const modalWindow = document.getElementById("modalWindow");
  const recipeDetailsModal = modalWindow?.querySelector(".modal");
  if (recipeDetailsModal) {
    const modalInstance = bootstrap.Modal.getInstance(recipeDetailsModal);
    if (modalInstance) {
      modalInstance.hide();
    }
  }
}

let currentRecipeId = null;

window.viewIngredientReview = async function (ingredientId) {
  console.log("Global viewIngredientReview called with ID:", ingredientId);

  const recipeModal = document.getElementById("recipeModal");
  if (recipeModal) {
    const recipeModalInstance = bootstrap.Modal.getInstance(recipeModal);
    if (recipeModalInstance) {
      recipeModalInstance.hide();
    }
  }

  setTimeout(async () => {
    const modal = document.getElementById("ingredientReviewModal");
    const content = document.getElementById("ingredientReviewContent");

    if (!modal || !content) {
      console.error("Modal or content container not found");
      showError("Modal components not found");
      return;
    }

    try {
      content.innerHTML = `
        <div class="text-center p-4">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <p class="mt-2">Loading ingredient details...</p>
        </div>
    `;

      const bootstrapModal = new bootstrap.Modal(modal);
      bootstrapModal.show();

      const response = await fetch(
        `/Admin/GetIngredientReview/${ingredientId}`,
        {
          method: "GET",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "XMLHttpRequest",
          },
        }
      );

      if (response.ok) {
        const html = await response.text();
        content.innerHTML = html;
      } else {
        content.innerHTML = `
            <div class="alert alert-danger">
                <h6>Error ${response.status}</h6>
                <p>Failed to load ingredient details.</p>
            </div>
        `;
      }
    } catch (error) {
      console.error("Error:", error);
      content.innerHTML = `
        <div class="alert alert-danger">
            <h6>Connection Error</h6>
            <p>${error.message}</p>
        </div>
    `;
    }
  }, 300);
};

function showRecipeModal() {
  if (currentRecipeId) {
    showRecipeDetails(currentRecipeId, true, "Buttons");
  } else {
    console.error("No recipe ID stored - cannot restore recipe modal");
  }
}

function showNotification(message, type) {
  if (type === "error") {
    showError(message);
  } else if (type === "success") {
    showSuccess(message);
  } else if (type === "warning") {
    showWarning(message);
  } else {
    showInfo(message);
  }
}

function openMealTagsModal() {
  fetch("/Admin/GetMealTagsPartial")
    .then((response) => response.text())
    .then((html) => {
      document.getElementById("mealTagsModalContainer").innerHTML = html;
      const modal = new bootstrap.Modal(
        document.getElementById("mealTagsModal")
      );
      modal.show();
      setTimeout(() => {
        loadMealKeywords();
      }, 100);
    })
    .catch((error) => {
      console.error("Error loading meal tags modal:", error);
      showError("Error loading meal tags");
    });
}

function openRestaurantMealsModal() {
  fetch("/Admin/GetRestaurantMealsPartial")
    .then((response) => response.text())
    .then((html) => {
      document.getElementById("restaurantMealsModalContainer").innerHTML = html;
      const modal = new bootstrap.Modal(
        document.getElementById("restaurantMealsModal")
      );
      modal.show();
      setTimeout(() => {
        loadRestaurants();
      }, 100);
    })
    .catch((error) => {
      console.error("Error loading restaurant meals modal:", error);
      showError("Error loading restaurant meals");
    });
}

function loadMealKeywords() {
  const container = document.getElementById("mealKeywordsList");
  if (!container) {
    console.error("mealKeywordsList container not found");
    return;
  }

  container.innerHTML = `
        <div class="text-center py-4">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    `;

  fetch("/Admin/GetMealKeywords")
    .then((response) => {
      if (!response.ok) {
        throw new Error("Network response was not ok");
      }
      return response.json();
    })
    .then((data) => {
      console.log("Loaded keywords:", data);
      displayMealKeywords(data);
    })
    .catch((error) => {
      console.error("Error loading meal keywords:", error);
      container.innerHTML =
        '<p class="text-danger text-center">Error loading keywords. Please try again.</p>';
      showError("Error loading meal keywords");
    });
}

function displayMealKeywords(keywords) {
  const container = document.getElementById("mealKeywordsList");
  if (!container) return;

  if (!keywords || keywords.length === 0) {
    container.innerHTML =
      '<p class="text-muted text-center py-4">No keywords found. Add your first keyword above!</p>';
    return;
  }

  const grouped = keywords.reduce((acc, keyword) => {
    if (!acc[keyword.tag]) {
      acc[keyword.tag] = [];
    }
    acc[keyword.tag].push(keyword);
    return acc;
  }, {});

  let html = "";
  const tagOrder = ["breakfast", "main", "snack"];

  tagOrder.forEach((tag) => {
    const items = grouped[tag];
    if (items && items.length > 0) {
      html += `
                <div class="tag-group mb-4">
                    <h5 class="tag-header">
                        <span class="badge bg-primary">${
                          tag.charAt(0).toUpperCase() + tag.slice(1)
                        }</span>
                    </h5>
                    <div class="keyword-list">
                        ${items
                          .map(
                            (item) => `
                            <div class="keyword-item">
                                <span class="keyword-name">${item.name}</span>
                                <button class="btn btn-sm btn-outline-danger" onclick="deleteMealKeyword(${
                                  item.id
                                }, '${item.name.replace(/'/g, "\\'")}')">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </div>
                        `
                          )
                          .join("")}
                    </div>
                </div>
            `;
    }
  });

  if (html === "") {
    container.innerHTML =
      '<p class="text-muted text-center py-4">No keywords found. Add your first keyword above!</p>';
  } else {
    container.innerHTML = html;
  }
}

function addMealKeyword() {
  const nameInput = document.getElementById("newKeywordName");
  const tagSelect = document.getElementById("newKeywordTag");

  if (!nameInput || !tagSelect) {
    console.error("Form elements not found");
    return;
  }

  const name = nameInput.value.trim();
  const tag = tagSelect.value;

  if (!name) {
    showError("Please enter a keyword name");
    return;
  }

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch("/Admin/AddMealKeyword", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
    body: JSON.stringify({ name: name, tag: tag }),
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showSuccess("Keyword added successfully");
        nameInput.value = "";
        loadMealKeywords();
      } else {
        showError(data.message || "Error adding keyword");
      }
    })
    .catch((error) => {
      console.error("Error adding keyword:", error);
      showError("Error adding keyword");
    });
}

function deleteMealKeyword(id, name) {
  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch(`/Admin/DeleteMealKeyword/${id}`, {
    method: "DELETE",
    headers: {
      RequestVerificationToken: token,
    },
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showError("Keyword deleted");
        loadMealKeywords();
      } else {
        showError(data.message || "Error deleting keyword");
      }
    })
    .catch((error) => {
      console.error("Error deleting keyword:", error);
      showError("Error deleting keyword");
    });
}

function loadRestaurants() {
  const select = document.getElementById("restaurantSelect");
  if (!select) {
    console.error("restaurantSelect not found");
    return;
  }

  select.innerHTML = '<option value="">Loading restaurants...</option>';

  fetch("/Admin/GetRestaurants")
    .then((response) => {
      if (!response.ok) {
        throw new Error("Network response was not ok");
      }
      return response.json();
    })
    .then((data) => {
      console.log("Loaded restaurants:", data);
      if (data && data.length > 0) {
        select.innerHTML =
          '<option value="">Select a restaurant...</option>' +
          data
            .map((r) => `<option value="${r.id}">${r.name}</option>`)
            .join("");
      } else {
        select.innerHTML = '<option value="">No restaurants found</option>';
      }
    })
    .catch((error) => {
      console.error("Error loading restaurants:", error);
      select.innerHTML = '<option value="">Error loading restaurants</option>';
      showError("Error loading restaurants");
    });
}

function loadRestaurantMeals(restaurantId) {
  const container = document.getElementById("restaurantMealsList");
  const addMealBtn = document.getElementById("showAddMealBtn");
  const editRestaurantBtn = document.getElementById("editRestaurantBtn");
  const deleteRestaurantBtn = document.getElementById("deleteRestaurantBtn");

  if (!container) {
    console.error("restaurantMealsList container not found");
    return;
  }

  if (!restaurantId) {
    container.innerHTML =
      '<p class="text-muted text-center">Please select a restaurant</p>';
    if (addMealBtn) addMealBtn.style.display = "none";
    if (editRestaurantBtn) editRestaurantBtn.style.display = "none";
    if (deleteRestaurantBtn) deleteRestaurantBtn.style.display = "none";
    return;
  }

  if (addMealBtn) addMealBtn.style.display = "block";
  if (editRestaurantBtn) editRestaurantBtn.style.display = "block";
  if (deleteRestaurantBtn) deleteRestaurantBtn.style.display = "block";

  container.innerHTML = `
        <div class="text-center py-4">
            <div class="spinner-border text-success" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    `;

  fetch(`/Admin/GetRestaurantMeals/${restaurantId}`)
    .then((response) => {
      if (!response.ok) {
        throw new Error("Network response was not ok");
      }
      return response.json();
    })
    .then((data) => {
      console.log("Loaded meals:", data);
      displayRestaurantMeals(data);
    })
    .catch((error) => {
      console.error("Error loading meals:", error);
      container.innerHTML =
        '<p class="text-danger text-center">Error loading meals. Please try again.</p>';
      showError("Error loading meals");
    });
}

function displayRestaurantMeals(meals) {
  const container = document.getElementById("restaurantMealsList");

  if (meals.length === 0) {
    container.innerHTML =
      '<p class="text-muted text-center">No meals found for this restaurant</p>';
    return;
  }

  let html = '<div class="meals-grid">';
  meals.forEach((meal) => {
    html += `
            <div class="meal-card">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="flex-grow-1">
                        <h6 class="meal-name">${meal.itemName}</h6>
                        <p class="meal-description text-muted small">${
                          meal.itemDescription || "No description"
                        }</p>
                        <div class="meal-macros mt-2">
                            <small>
                                <strong>${meal.calories}</strong> cal | 
                                <strong>${meal.protein}g</strong> protein | 
                                <strong>${meal.carbs}g</strong> carbs | 
                                <strong>${meal.fat}g</strong> fat
                            </small>
                        </div>
                    </div>
                    <button class="btn btn-sm btn-outline-primary me-2" 
                            onclick="editRestaurantMeal(${meal.id})">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn btn-sm btn-outline-danger" onclick="deleteRestaurantMeal(${
                      meal.id
                    }, '${meal.itemName.replace(/'/g, "\\'")}')">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        `;
  });
  html += "</div>";

  container.innerHTML = html;
}

function showAddMealForm() {
  document.getElementById("addMealFormSection").style.display = "block";
  document.getElementById("showAddMealBtn").style.display = "none";
  document.getElementById("addMealFormTitle").innerHTML =
    '<i class="fas fa-plus-circle me-2"></i>Add New Meal';

  const btn = document.querySelector("#addMealForm button.btn-success");
  btn.innerHTML = '<i class="fas fa-check me-2"></i>Add Meal';
  btn.setAttribute("onclick", "addRestaurantMeal()");
}

function cancelAddMeal() {
  document.getElementById("addMealFormSection").style.display = "none";
  document.getElementById("showAddMealBtn").style.display = "block";
  document.getElementById("addMealForm").reset();
  document.getElementById("editMealId").value = "";
}

function addRestaurantMeal() {
  const restaurantId = document.getElementById("restaurantSelect").value;

  if (!restaurantId) {
    showError("Please select a restaurant first");
    return;
  }

  const mealData = {
    restaurantId: parseInt(restaurantId),
    itemName: document.getElementById("mealItemName").value.trim(),
    itemDescription: document
      .getElementById("mealItemDescription")
      .value.trim(),
    type: [],
    calories: parseFloat(document.getElementById("mealCalories").value),
    protein: parseFloat(document.getElementById("mealProtein").value),
    carbs: parseFloat(document.getElementById("mealCarbs").value),
    fat: parseFloat(document.getElementById("mealFat").value),
  };

  if (!mealData.itemName) {
    showError("Please enter a meal name");
    return;
  }

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch("/Admin/AddRestaurantMeal", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
    body: JSON.stringify(mealData),
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showSuccess("Meal added successfully");
        cancelAddMeal();
        loadRestaurantMeals(restaurantId);
      } else {
        showError(data.message || "Error adding meal");
      }
    })
    .catch((error) => {
      console.error("Error adding meal:", error);
      showError("Error adding meal");
    });
}

function deleteRestaurantMeal(id, name) {
  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;
  const restaurantId = document.getElementById("restaurantSelect").value;

  fetch(`/Admin/DeleteRestaurantMeal/${id}`, {
    method: "DELETE",
    headers: {
      RequestVerificationToken: token,
    },
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showError("Meal deleted");
        loadRestaurantMeals(restaurantId);
      } else {
        showError(data.message || "Error deleting meal");
      }
    })
    .catch((error) => {
      console.error("Error deleting meal:", error);
      showError("Error deleting meal");
    });
}

function editRestaurantMeal(id) {
  const restaurantId = document.getElementById("restaurantSelect").value;

  fetch(`/Admin/GetRestaurantMeals/${restaurantId}`)
    .then((res) => res.json())
    .then((meals) => {
      const meal = meals.find((m) => m.id === id);
      if (!meal) {
        showError("Meal not found");
        return;
      }

      document.getElementById("editMealId").value = meal.id;
      document.getElementById("mealItemName").value = meal.itemName;
      document.getElementById("mealItemDescription").value =
        meal.itemDescription || "";
      document.getElementById("mealCalories").value = meal.calories;
      document.getElementById("mealProtein").value = meal.protein;
      document.getElementById("mealCarbs").value = meal.carbs;
      document.getElementById("mealFat").value = meal.fat;


      document.getElementById("addMealFormSection").style.display = "block";
      document.getElementById("showAddMealBtn").style.display = "none";
      document.getElementById("addMealFormTitle").innerHTML =
        '<i class="fas fa-edit me-2"></i>Edit Meal';

      const btn = document.querySelector("#addMealForm button.btn-success");
      btn.innerHTML = '<i class="fas fa-save me-2"></i>Update Meal';
      btn.setAttribute("onclick", "updateRestaurantMeal()");
    })
    .catch((err) => {
      console.error("Error fetching meals:", err);
      showError("Error loading meals");
    });
}

function updateRestaurantMeal() {
  const restaurantId = document.getElementById("restaurantSelect").value;
  const id = parseInt(document.getElementById("editMealId").value);

  const mealData = {
    id: id,
    restaurantId: parseInt(restaurantId),
    itemName: document.getElementById("mealItemName").value.trim(),
    itemDescription: document
      .getElementById("mealItemDescription")
      .value.trim(),
    type: [],
    calories: parseFloat(document.getElementById("mealCalories").value),
    protein: parseFloat(document.getElementById("mealProtein").value),
    carbs: parseFloat(document.getElementById("mealCarbs").value),
    fat: parseFloat(document.getElementById("mealFat").value),
  };

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch("/Admin/EditRestaurantMeal", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
    body: JSON.stringify(mealData),
  })
    .then((res) => res.json())
    .then((data) => {
      if (data.success) {
        showSuccess("Meal updated successfully");
        cancelAddMeal();
        loadRestaurantMeals(restaurantId);
      } else {
        showError(data.message || "Error updating meal");
      }
    })
    .catch((err) => {
      console.error("Error updating meal:", err);
      showError("Error updating meal");
    });
}

function showAddRestaurantForm() {
  document.getElementById("addRestaurantFormSection").style.display = "block";
  document.getElementById("addMealFormSection").style.display = "none";
  document.getElementById("showAddMealBtn").style.display = "none";
  document.getElementById("editRestaurantBtn").style.display = "none";
  document.getElementById("deleteRestaurantBtn").style.display = "none";
  document.getElementById("restaurantFormTitle").innerHTML =
    '<i class="fas fa-store-alt me-2"></i>Add New Restaurant';

  const btn = document.querySelector("#addRestaurantForm button.btn-success");
  btn.innerHTML = '<i class="fas fa-check me-2"></i>Add Restaurant';
  btn.setAttribute("onclick", "addRestaurant()");

  document.getElementById("editRestaurantId").value = "";
  document.getElementById("addRestaurantForm").reset();
  document.getElementById("imagePreview").style.display = "none";
}

function cancelAddRestaurant() {
  document.getElementById("addRestaurantFormSection").style.display = "none";
  document.getElementById("addRestaurantForm").reset();
  document.getElementById("editRestaurantId").value = "";
  document.getElementById("imagePreview").style.display = "none";

  const restaurantId = document.getElementById("restaurantSelect").value;
  if (restaurantId) {
    document.getElementById("showAddMealBtn").style.display = "block";
    document.getElementById("editRestaurantBtn").style.display = "block";
    document.getElementById("deleteRestaurantBtn").style.display = "block";
  }
}

function previewImage() {
  const fileInput = document.getElementById("restaurantImage");
  const preview = document.getElementById("imagePreview");
  const previewImg = document.getElementById("previewImg");

  if (fileInput.files && fileInput.files[0]) {
    const reader = new FileReader();

    reader.onload = function (e) {
      previewImg.src = e.target.result;
      preview.style.display = "block";
    };

    reader.readAsDataURL(fileInput.files[0]);
  }
}

function addRestaurant() {
  const name = document.getElementById("restaurantName").value.trim();
  const description = document
    .getElementById("restaurantDescription")
    .value.trim();
  const imageFile = document.getElementById("restaurantImage").files[0];

  if (!name) {
    showError("Please enter a restaurant name");
    return;
  }

  if (!imageFile) {
    showError("Please select an image");
    return;
  }

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;
  const formData = new FormData();
  formData.append("name", name);
  formData.append("description", description);
  formData.append("image", imageFile);

  fetch("/Admin/AddRestaurant", {
    method: "POST",
    headers: {
      RequestVerificationToken: token,
    },
    body: formData,
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showSuccess("Restaurant added successfully");

        cancelAddRestaurant();
        loadRestaurants();

        setTimeout(() => {
          showAddMealForm();

          const select = document.getElementById("restaurantSelect");
          select.value = data.restaurantId;
        }, 500);
      } else {
        showError(data.message || "Error adding restaurant");
      }
    })
    .catch((error) => {
      console.error("Error adding restaurant:", error);
      showError("Error adding restaurant");
    });
}

function showEditRestaurantForm() {
  const restaurantId = document.getElementById("restaurantSelect").value;
  if (!restaurantId) {
    showError("Please select a restaurant");
    return;
  }

  fetch(`/Admin/GetRestaurant/${restaurantId}`)
    .then((response) => response.json())
    .then((restaurant) => {
      document.getElementById("editRestaurantId").value = restaurant.id;
      document.getElementById("restaurantName").value = restaurant.name;
      document.getElementById("restaurantDescription").value =
        restaurant.description || "";

      if (restaurant.imageUrl) {
        const previewImg = document.getElementById("previewImg");
        previewImg.src = restaurant.imageUrl;
        document.getElementById("imagePreview").style.display = "block";
      }

      document.getElementById("addRestaurantFormSection").style.display =
        "block";
      document.getElementById("addMealFormSection").style.display = "none";
      document.getElementById("showAddMealBtn").style.display = "none";
      document.getElementById("editRestaurantBtn").style.display = "none";
      document.getElementById("deleteRestaurantBtn").style.display = "none";
      document.getElementById("restaurantFormTitle").innerHTML =
        '<i class="fas fa-edit me-2"></i>Edit Restaurant';

      const btn = document.querySelector(
        "#addRestaurantForm button.btn-success"
      );
      btn.innerHTML = '<i class="fas fa-save me-2"></i>Update Restaurant';
      btn.setAttribute("onclick", "updateRestaurant()");
    })
    .catch((error) => {
      console.error("Error loading restaurant:", error);
      showError("Error loading restaurant");
    });
}

function updateRestaurant() {
  const id = parseInt(document.getElementById("editRestaurantId").value);
  const name = document.getElementById("restaurantName").value.trim();
  const description = document
    .getElementById("restaurantDescription")
    .value.trim();
  const imageFile = document.getElementById("restaurantImage").files[0];

  if (!name) {
    showError("Please enter a restaurant name");
    return;
  }

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;
  const formData = new FormData();
  formData.append("id", id);
  formData.append("name", name);
  formData.append("description", description);
  if (imageFile) {
    formData.append("image", imageFile);
  }

  fetch("/Admin/EditRestaurant", {
    method: "POST",
    headers: {
      RequestVerificationToken: token,
    },
    body: formData,
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showSuccess("Restaurant updated successfully");
        cancelAddRestaurant();
        loadRestaurants();

        setTimeout(() => {
          const select = document.getElementById("restaurantSelect");
          select.value = id;
          loadRestaurantMeals(id);
        }, 500);
      } else {
        showError(data.message || "Error updating restaurant");
      }
    })
    .catch((error) => {
      console.error("Error updating restaurant:", error);
      showError("Error updating restaurant");
    });
}

function deleteRestaurant() {
  const restaurantId = document.getElementById("restaurantSelect").value;
  if (!restaurantId) {
    showError("Please select a restaurant");
    return;
  }

  const token = document.querySelector(
    'input[name="__RequestVerificationToken"]'
  ).value;

  fetch(`/Admin/DeleteRestaurant/${restaurantId}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      RequestVerificationToken: token,
    },
  })
    .then((response) => response.json())
    .then((data) => {
      if (data.success) {
        showError(data.message || "Restaurant deleted");

        const restaurantSelect = document.getElementById("restaurantSelect");
        restaurantSelect.remove(restaurantSelect.selectedIndex);
        restaurantSelect.value = "";

        document.getElementById("addRestaurantFormSection").style.display =
          "none";
        document.getElementById("addMealFormSection").style.display = "none";
        document.getElementById("editRestaurantBtn").style.display = "none";
        document.getElementById("deleteRestaurantBtn").style.display = "none";
        document.getElementById("showAddMealBtn").style.display = "none";

        const mealsContainer = document.getElementById("restaurantMealsList");
        if (mealsContainer) {
          mealsContainer.innerHTML =
            '<p class="text-muted text-center">Please select a restaurant</p>';
        }
      } else {
        showError(data.message || "Error deleting restaurant");
      }
    })
    .catch((error) => {
      console.error("Error deleting restaurant:", error);
      showError("Error deleting restaurant");
    });
}
