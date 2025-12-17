let selectedIngredients = [];
let selectedInstructions = [];
let searchTimeout;
let currentFocus = -1;
let selectedIngredient = null;

const ingredientSearch = document.getElementById("ingredientSearch");
const ingredientDropdown = document.getElementById("ingredientDropdown");
const ingredientsList = document.getElementById("ingredientsList");
const hiddenIngredientsInput = document.getElementById("selectedIngredients");
const addButton = document.getElementById("addIngredientButton");
const qtyInput = document.getElementById("ingredientQuantity");
const unitSelect = document.getElementById("ingredientUnit");
const instructionInput = document.getElementById("instructionInput");
const addInstructionButton = document.getElementById("addInstructionButton");
const instructionsList = document.getElementById("instructionsList");
const hiddenInstructionsInput = document.getElementById("selectedInstructions");
const fileUploadArea = document.getElementById("fileUploadArea");
const fileInput = document.getElementById("RecipeImage");
const imagePreview = document.getElementById("imagePreview");

document.addEventListener("DOMContentLoaded", function () {
  initializeSearchFunctionality();
  initializeInstructionsFunctionality();
  initializeInstructionsAndIngredients();

  updateIngredientsDisplay();
  updateIngredientsInput();
  updateInstructionsDisplay();
  updateInstructionsInput();
});

function initializeInstructionsAndIngredients() {
  var existingInstructions = JSON.parse(
    document.getElementById("selectedInstructionsScript").innerText
  )[0];
  existingInstructions = JSON.parse(existingInstructions);

  var existingIngredients = JSON.parse(
    document.getElementById("selectedIngredientsScript").innerText
  );
  console.log(existingIngredients);

  existingIngredients.forEach((element) => {
    selectedIngredients.push({
      Id: element.IngredientId,
      Name: element.Ingredient.Name,
      Quantity: element.Quantity,
      Unit: element.Unit,
    });
  });

  existingInstructions.forEach((element) => {
    selectedInstructions.push(element);
  });
}

function initializeSearchFunctionality() {
  if (addButton) {
    addButton.addEventListener("click", function () {
      if (selectedIngredient && qtyInput && unitSelect) {
        const qty = parseFloat(qtyInput.value);
        const unit = unitSelect.value;

        if (qty > 0 && unit && selectedIngredient.name) {
          addIngredient(selectedIngredient, qty, unit);
        } else {
          alert("Please enter a valid quantity and select a unit.");
        }
      } else {
        alert("Please search and select an ingredient first.");
      }
    });
  }

  if (ingredientSearch) {
    ingredientSearch.addEventListener("input", function () {
      const query = this.value.trim();
      currentFocus = -1;
      selectedIngredient = null;

      if (query === "") {
        hideDropdown(ingredientDropdown);
        return;
      }

      clearTimeout(searchTimeout);
      searchTimeout = setTimeout(() => {
        searchIngredients(query);
      }, 300);
    });

    ingredientSearch.addEventListener("keydown", function (e) {
      handleKeyNavigation(e, ingredientDropdown);
    });

    ingredientSearch.addEventListener("focus", function () {
      if (this.value.trim() !== "") {
        searchIngredients(this.value.trim());
      }
    });
  }

  document.addEventListener("click", function (e) {
    if (!e.target.closest(".search-container")) {
      hideDropdown(ingredientDropdown);
    }
  });
}

function initializeInstructionsFunctionality() {
  if (addInstructionButton) {
    addInstructionButton.addEventListener("click", function () {
      addInstruction();
    });
  }

  if (instructionInput) {
    instructionInput.addEventListener("keydown", function (e) {
      if (e.key === "Enter") {
        e.preventDefault();
        addInstruction();
      }
    });
  }
}

async function searchIngredients(query) {
  if (!ingredientDropdown) return;

  try {
    ingredientDropdown.innerHTML = '<div class="loading">Loading...</div>';
    showDropdown(ingredientDropdown);

    const response = await fetch(
      `/Recipes/getSuggestions?query=${encodeURIComponent(query)}`,
      {
        method: "GET",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "XMLHttpRequest",
        },
      }
    );

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const suggestions = await response.json();
    displaySuggestions(suggestions, query);
  } catch (error) {
    console.error("Error fetching suggestions:", error);
    ingredientDropdown.innerHTML =
      '<div class="no-results">Error loading suggestions. Please try again.</div>';
    showDropdown(ingredientDropdown);
  }
}

function displaySuggestions(suggestions, query) {
  if (!ingredientDropdown) return;

  ingredientDropdown.innerHTML = "";

  if (!suggestions || suggestions.length === 0) {
    ingredientDropdown.innerHTML =
      '<div class="no-results">No results found</div>';
    showDropdown(ingredientDropdown);
    return;
  }

  suggestions.forEach((suggestion, index) => {
    const item = document.createElement("div");
    item.className = "dropdown-item";
    item.setAttribute("data-index", index);

    const name = typeof suggestion === "string" ? suggestion : suggestion.name;
    const id = typeof suggestion === "object" ? suggestion.id : null;

    const regex = new RegExp(`(${query})`, "gi");
    const highlightedText = name.replace(regex, "<strong>$1</strong>");
    item.innerHTML = highlightedText;

    item.addEventListener("click", function () {
      selectedIngredient = {
        id: id,
        name: name,
      };
      ingredientSearch.value = name;
      hideDropdown(ingredientDropdown);
    });

    ingredientDropdown.appendChild(item);
  });

  showDropdown(ingredientDropdown);
}

function handleKeyNavigation(e, dropdownElement) {
  if (!dropdownElement) return;

  const items = dropdownElement.querySelectorAll(
    ".dropdown-item:not(.no-results):not(.loading)"
  );

  switch (e.key) {
    case "ArrowDown":
      e.preventDefault();
      currentFocus++;
      if (currentFocus >= items.length) currentFocus = 0;
      setActive(items);
      break;

    case "ArrowUp":
      e.preventDefault();
      currentFocus--;
      if (currentFocus < 0) currentFocus = items.length - 1;
      setActive(items);
      break;

    case "Enter":
      e.preventDefault();
      if (currentFocus > -1 && items[currentFocus]) {
        items[currentFocus].click();
      }
      break;

    case "Escape":
      hideDropdown(dropdownElement);
      e.target.blur();
      break;
  }
}

function setActive(items) {
  items.forEach((item) => item.classList.remove("highlighted"));

  if (currentFocus >= 0 && currentFocus < items.length) {
    items[currentFocus].classList.add("highlighted");
    items[currentFocus].scrollIntoView({ block: "nearest" });
  }
}

function addIngredient(ingredient, quantity, unit) {
  if (!ingredient || !ingredient.name) {
    console.error("Invalid ingredient data");
    return;
  }

  const existingIngredient = selectedIngredients.find(
    (item) => item.Id === ingredient.id
  );

  if (existingIngredient) {
    alert("This ingredient is already added to the recipe.");
    return;
  }

  const newIngredient = {
    Id: ingredient.id,
    Name: ingredient.name,
    Quantity: quantity,
    Unit: unit,
  };

  selectedIngredients.push(newIngredient);
  updateIngredientsDisplay();
  updateIngredientsInput();

  if (ingredientSearch) ingredientSearch.value = "";
  if (qtyInput) qtyInput.value = "";
  if (unitSelect) unitSelect.value = "";

  selectedIngredient = null;
  hideDropdown(ingredientDropdown);
  currentFocus = -1;
}

function removeIngredient(ingredientName) {
  selectedIngredients = selectedIngredients.filter(
    (item) => item.Name !== ingredientName
  );
  updateIngredientsDisplay();
  updateIngredientsInput();
}

function addInstruction() {
  if (!instructionInput) return;

  const instruction = instructionInput.value.trim();

  if (instruction === "") {
    alert("Please enter an instruction.");
    return;
  }

  if (selectedInstructions.includes(instruction)) {
    alert("This instruction is already added.");
    return;
  }

  selectedInstructions.push(instruction);
  updateInstructionsDisplay();
  updateInstructionsInput();

  instructionInput.value = "";
}

function removeInstruction(instructionIndex) {
  selectedInstructions.splice(instructionIndex, 1);
  updateInstructionsDisplay();
  updateInstructionsInput();
}

function updateIngredientsDisplay() {
  if (!ingredientsList) return;

  if (selectedIngredients.length === 0) {
    ingredientsList.innerHTML =
      '<small class="text-muted">Selected ingredients will appear here</small>';
  } else {
    ingredientsList.innerHTML = selectedIngredients
      .map(
        (ingredient) =>
          `<span class="ingredient-tag">
                ${ingredient.Quantity} ${ingredient.Unit} of ${ingredient.Name}
                <span class="remove" onclick="removeIngredient('${ingredient.Name}')" title="Remove ingredient">&times;</span>
            </span>`
      )
      .join("");
  }
}

function updateInstructionsDisplay() {
  if (!instructionsList) return;

  if (selectedInstructions.length === 0) {
    instructionsList.innerHTML =
      '<small class="text-muted">Added instructions will appear here</small>';
  } else {
    instructionsList.innerHTML = selectedInstructions
      .map(
        (instruction, index) =>
          `<div class="instruction-item">
                <span class="instruction-number" ">${index + 1}</span>
                <span class="instruction-text">${instruction}</span>
                <span class="remove" onclick="removeInstruction(${index})" style=font-size: 18px;"title="Remove instruction">&times;</span>
            </div>`
      )
      .join("");
  }
}

function updateIngredientsInput() {
  if (hiddenIngredientsInput) {
    hiddenIngredientsInput.value = JSON.stringify(selectedIngredients);
    console.log(
      "Updated hidden ingredients input:",
      hiddenIngredientsInput.value
    );
  }
}

function updateInstructionsInput() {
  if (hiddenInstructionsInput) {
    hiddenInstructionsInput.value = JSON.stringify(selectedInstructions);
    console.log(
      "Updated hidden instructions input:",
      hiddenInstructionsInput.value
    );
  }
}

function showDropdown(dropdownElement) {
  if (dropdownElement) {
    dropdownElement.style.display = "block";
  }
}

function hideDropdown(dropdownElement) {
  if (dropdownElement) {
    dropdownElement.style.display = "none";
  }
  currentFocus = -1;
}

fileUploadArea.addEventListener("click", () => fileInput.click());

fileUploadArea.addEventListener("dragover", (e) => {
  e.preventDefault();
  fileUploadArea.classList.add("dragover");
});

fileUploadArea.addEventListener("dragleave", () => {
  fileUploadArea.classList.remove("dragover");
});

fileUploadArea.addEventListener("drop", (e) => {
  e.preventDefault();
  fileUploadArea.classList.remove("dragover");
  const files = e.dataTransfer.files;
  if (files.length > 0) {
    handleFileSelect(files[0]);
    setFileToInput(files[0]);
  }
});

fileInput.addEventListener("change", (e) => {
  if (e.target.files && e.target.files[0]) {
    handleFileSelect(e.target.files[0]);
  }
});

function handleFileSelect(file) {
  if (file.type.startsWith("image/")) {
    const reader = new FileReader();
    reader.onload = (e) => {
      imagePreview.innerHTML = `
                <img id="image-preview" src="${e.target.result}" alt="Recipe preview">
                <p style="margin-top: 0.5rem; color: #4CAF50; font-weight: 500;">✓ Image uploaded successfully</p>
            `;
    };
    reader.readAsDataURL(file);
  }
}

function setFileToInput(file) {
  const dataTransfer = new DataTransfer();
  dataTransfer.items.add(file);

  fileInput.files = dataTransfer.files;
}
