document.addEventListener('DOMContentLoaded', function () {
        const regeneratedMeals = document.querySelectorAll('.meal-card[data-is-regenerated="true"][data-is-viewed="false"]');

        if (regeneratedMeals.length > 0) {
            const mealIds = Array.from(regeneratedMeals).map(card => parseInt(card.dataset.mealId));

            fetch('@Url.Action("MarkMealsAsViewed", "MealPlan")', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify(mealIds)
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        console.log('Regenerated meals marked as viewed');

                        regeneratedMeals.forEach(card => {
                            card.dataset.isViewed = 'true';
                            const indicator = card.querySelector('.regenerated-indicator');
                            if (indicator) {
                                indicator.classList.add('fade-out');
                                setTimeout(() => {
                                    indicator.remove();
                                }, 300);
                            }
                        });
                    }
                })
                .catch(error => {
                    console.error('Error marking meals as viewed:', error);
                });
        }

        const cards = document.querySelectorAll('.day-card');
        cards.forEach((card, index) => {
            card.style.animationDelay = `${index * 0.1}s`;
        });
    });




    function regenerateMeal(event, mealSlotId, mealPlanId) {
        event.stopPropagation();
        const btn = event.currentTarget;
        const card = btn.closest('.meal-card');
        btn.disabled = true;
        btn.classList.add('loading');
        btn.innerHTML = '<i class="fas fa-sync-alt"></i> Regenerating...';
        
        const formData = new FormData();
        formData.append('mealSlotId', mealSlotId);
        formData.append('mealPlanId', mealPlanId);
        formData.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);
        
        fetch('/MealPlan/RegenerateMeal', {
            method: 'POST',
            body: formData
        })
        .then(response => {
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            return response.json();
        })
        .then(data => {
            if (data.success) {
                const existingIndicator = card.querySelector('.regenerated-indicator');
                if (!existingIndicator) {
                    const indicator = document.createElement('div');
                    indicator.className = 'regenerated-indicator';
                    indicator.innerHTML = '<i class="fas fa-sync-alt"></i>';
                    card.appendChild(indicator);
                }
                setTimeout(() => {
                    location.reload();
                }, 1500);
            } else {
                alert(data.message || 'Failed to regenerate meal');
                btn.disabled = false;
                btn.classList.remove('loading');
                btn.innerHTML = '<i class="fas fa-sync-alt"></i> Regenerate';
            }
        })
        .catch(error => {
            console.error('Error:', error);
            alert('An error occurred while regenerating the meal');
            btn.disabled = false;
            btn.classList.remove('loading');
            btn.innerHTML = '<i class="fas fa-sync-alt"></i> Regenerate';
        });
    }

    function showRecipeDetailsFromMealPlan(recipeId) {
        const clickedCard = event.currentTarget;
        clickedCard.classList.add('loading');

        fetch(`/Recipes/Details/${recipeId}`)
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.text();
            })
            .then(html => {
                const modalContainer = document.getElementById('modalWindow');
                modalContainer.innerHTML = html;

                const scripts = modalContainer.querySelectorAll("script");
                scripts.forEach(script => {
                    const newScript = document.createElement("script");
                    if (script.src) {
                        newScript.src = script.src;
                    } else {
                        newScript.textContent = script.textContent;
                    }
                    document.body.appendChild(newScript);
                    document.body.removeChild(newScript);
                });

                const modalElement = modalContainer.querySelector('.modal');
                if (modalElement) {
                    const modal = new bootstrap.Modal(modalElement);
                    modal.show();

                    modalElement.addEventListener('hidden.bs.modal', function () {
                        modalContainer.innerHTML = '';
                        clickedCard.classList.remove('loading');
                    });

                    modalElement.addEventListener('shown.bs.modal', function () {
                        clickedCard.classList.remove('loading');
                    });
                } else {
                    clickedCard.classList.remove('loading');
                }
            })
            .catch(err => {
                console.error("Failed to fetch recipe details", err);
                alert("Failed to load recipe details. Please try again.");
                clickedCard.classList.remove('loading');
            });
    }

    function handleMealCardClick(event, recipeId, mealSlotId) {
        SetIsViewed(event, true, mealSlotId);

        if (event.target.closest('.regenerate-btn')) {
            return;
        }

        if (recipeId && recipeId > 0) {
            showRecipeDetailsFromMealPlan(recipeId);
        }
    }

    function SetIsViewed(event, isViewed, mealSlotId) {
        console.log('SetIsViewed called with mealSlotId:', mealSlotId);
        const card = event.currentTarget;
        const currentlyViewed = card.dataset.isViewed === 'true';

        if (isViewed && !currentlyViewed) {
            fetch(`/MealPlan/MarkMealsAsViewed?mealId=${mealSlotId}`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                }
            })
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        console.log('Meal marked as viewed');
                        card.dataset.isViewed = 'true';

                        const indicator = card.querySelector('.regenerated-indicator');
                        if (indicator) {
                            indicator.classList.add('fade-out');
                            setTimeout(() => indicator.remove(), 300);
                        }
                    } else {
                        console.error(data.message);
                    }
                })
                .catch(error => console.error('Error marking meal as viewed:', error));
        }
    }

    function showDeleteModal() {
        document.getElementById('deleteModal').style.display = 'block';
        document.body.style.overflow = 'hidden';
    }

    function hideDeleteModal() {
        document.getElementById('deleteModal').style.display = 'none';
        document.body.style.overflow = 'auto';
    }

    function confirmDelete() {
        document.getElementById('deleteForm').submit();
    }

    window.onclick = function (event) {
        const modal = document.getElementById('deleteModal');
        if (event.target === modal) {
            hideDeleteModal();
        }
    }

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            hideDeleteModal();
        }
    });