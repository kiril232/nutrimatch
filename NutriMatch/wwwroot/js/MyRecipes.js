document.addEventListener("DOMContentLoaded", function () {
  const urlParams = new URLSearchParams(window.location.search);
  const openDeclineModalRecipeId = urlParams.get("openDeclineModal");

  if (openDeclineModalRecipeId) {
    setTimeout(function () {
      showRecipeDetails(openDeclineModalRecipeId, true, "Declined");
    }, 300);

    const newUrl = window.location.pathname;
    window.history.replaceState({}, document.title, newUrl);
  }
});

function showRecipeDetails(recipeId, isOwner, recipeStatus) {
  const clickedCard = event && event.currentTarget ? event.currentTarget : null;

  if (clickedCard) {
    clickedCard.classList.add("loading");
  }

  const params = new URLSearchParams({
    isOwner: isOwner,
    recipeDetailsDisplayContorol: recipeStatus,
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

      const modalElement = modalContainer.querySelector(".modal");
      if (modalElement) {
        const modal = new bootstrap.Modal(modalElement);
        modal.show();

        modalElement.addEventListener("hidden.bs.modal", function () {
          modalContainer.innerHTML = "";
          if (clickedCard) {
            clickedCard.classList.remove("loading");
          }
        });

        modalElement.addEventListener("shown.bs.modal", function () {
          if (clickedCard) {
            clickedCard.classList.remove("loading");
          }
        });
      } else {
        if (clickedCard) {
          clickedCard.classList.remove("loading");
        }
      }
    })
    .catch((err) => {
      console.error("Failed to fetch recipe details", err);
      alert("Failed to load recipe details. Please try again.");
      if (clickedCard) {
        clickedCard.classList.remove("loading");
      }
    });
}

function openDeleteModal(recipeId) {
  const deleteButton = event.target.closest("button");
  deleteButton.classList.add("loading");

  const recipeModalContainer = document.getElementById("modalWindow");
  const recipeModalElement = recipeModalContainer.querySelector(".modal");
  const savedRecipeHtml = recipeModalContainer.innerHTML;

  let recipeModalWasOpen = false;
  if (recipeModalElement && recipeModalElement.classList.contains("show")) {
    const recipeModalInstance = bootstrap.Modal.getInstance(recipeModalElement);
    if (recipeModalInstance) {
      recipeModalInstance.hide();
      recipeModalWasOpen = true;
    }
  }

  fetch(`/Recipes/Delete/${recipeId}`)
    .then((response) => response.text())
    .then((html) => {
      const deleteModalContainer = document.getElementById("modalWindowDelete");
      deleteModalContainer.innerHTML = html;

      const deleteModalElement = deleteModalContainer.querySelector(".modal");
      if (deleteModalElement) {
        const deleteModal = new bootstrap.Modal(deleteModalElement);
        deleteModal.show();

        deleteModalElement.addEventListener("hidden.bs.modal", function () {
          deleteButton.classList.remove("loading");
          deleteModalContainer.innerHTML = "";

          if (recipeModalWasOpen && savedRecipeHtml.trim() !== "") {
            recipeModalContainer.innerHTML = savedRecipeHtml;
            const restoredModal = recipeModalContainer.querySelector(".modal");
            if (restoredModal) {
              const restoredInstance = new bootstrap.Modal(restoredModal);
              restoredInstance.show();
            }
          }
        });

        deleteModalElement.addEventListener("shown.bs.modal", function () {
          deleteButton.classList.remove("loading");
        });
      } else {
        deleteButton.classList.remove("loading");
      }
    })
    .catch((error) => {
      console.error("Error loading delete modal:", error);
      deleteButton.classList.remove("loading");
      location.href = `/Recipes/Delete/${recipeId}`;
    });
}
