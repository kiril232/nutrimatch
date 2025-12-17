using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class MealPlanService : IMealPlanService
    {
        private readonly AppDbContext _context;
        private readonly Random _random;
        private readonly Dictionary<string, float> _mealTypeDistribution;

        public MealPlanService(AppDbContext context)
        {
            _context = context;
            _random = new Random();

            _mealTypeDistribution = new Dictionary<string, float>
            {
                { "breakfast", 0.25f },
                { "lunch", 0.35f },
                { "dinner", 0.35f },
                { "snack", 0.05f }
            };
        }

        public async Task<MealPlanResult> GenerateWeeklyMealPlanAsync(string userId, MealPlanRequest request)
        {
            var result = new MealPlanResult { Success = false };

            try
            {
                var weeklyPlan = new WeeklyMealPlan
                {
                    UserId = userId,
                    GeneratedAt = DateTime.UtcNow
                };

                var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
                var mealTypes = new[] { "breakfast", "lunch", "dinner" };

                var restaurantMealSlots = DistributeRestaurantMeals(request.RestaurantMealsPerWeek, days, mealTypes);

                foreach (var day in days)
                {
                    var usedRecipeIds = new HashSet<int>();
                    var usedRestaurantMealIds = new HashSet<int>();

                    var dailyMacros = new DailyMacros
                    {
                        Calories = request.DailyCalories,
                        Protein = request.DailyProtein,
                        Carbs = request.DailyCarbs,
                        Fat = request.DailyFat
                    };

                    foreach (var mealType in mealTypes)
                    {
                        var mealSlot = new MealSlot
                        {
                            Day = day,
                            MealType = mealType
                        };

                        var targetMacros = CalculateMealMacros(dailyMacros, mealType);
                        var isRestaurantMeal = restaurantMealSlots.Contains($"{day}_{mealType}");

                        if (isRestaurantMeal)
                        {
                            var restaurantMeal = await SelectRestaurantMealAsync(mealType, targetMacros, usedRestaurantMealIds);
                            if (restaurantMeal != null)
                            {
                                mealSlot.RestaurantMeal = restaurantMeal;
                                mealSlot.IsRestaurantMeal = true;
                                usedRestaurantMealIds.Add(restaurantMeal.Id);
                            }
                            else
                            {
                                var recipe = await SelectRecipeAsync(mealType, targetMacros, usedRecipeIds);
                                if (recipe != null)
                                {
                                    mealSlot.Recipe = recipe;
                                    mealSlot.IsRestaurantMeal = false;
                                    usedRecipeIds.Add(recipe.Id);
                                }
                            }
                        }
                        else
                        {
                            var recipe = await SelectRecipeAsync(mealType, targetMacros, usedRecipeIds);
                            if (recipe != null)
                            {
                                mealSlot.Recipe = recipe;
                                mealSlot.IsRestaurantMeal = false;
                                usedRecipeIds.Add(recipe.Id);
                            }
                        }

                        weeklyPlan.MealSlots.Add(mealSlot);
                    }

                    var remainingCalories = CalculateRemainingCalories(weeklyPlan.MealSlots.Where(ms => ms.Day == day).ToList(), dailyMacros.Calories);
                    if (remainingCalories > 100)
                    {
                        var snackMacros = new DailyMacros
                        {
                            Calories = remainingCalories,
                            Protein = remainingCalories * 0.15f / 4,
                            Carbs = remainingCalories * 0.50f / 4,
                            Fat = remainingCalories * 0.35f / 9
                        };

                        var snackSlot = new MealSlot
                        {
                            Day = day,
                            MealType = "snack"
                        };

                        var snackRecipe = await SelectRecipeAsync("snack", snackMacros, usedRecipeIds);
                        if (snackRecipe != null)
                        {
                            snackSlot.Recipe = snackRecipe;
                            snackSlot.IsRestaurantMeal = false;
                            weeklyPlan.MealSlots.Add(snackSlot);
                        }
                    }
                }

                _context.WeeklyMealPlans.Add(weeklyPlan);
                await _context.SaveChangesAsync();

                result.WeeklyMealPlan = weeklyPlan;
                result.DailyMacroTotals = CalculateDailyMacroTotals(weeklyPlan);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Failed to generate meal plan: {ex.Message}";
            }

            return result;
        }

        public async Task<bool> DeleteMealPlanAsync(int id, string userId)
        {
            try
            {
                var mealPlan = await _context.WeeklyMealPlans
                    .Include(wmp => wmp.MealSlots)
                    .FirstOrDefaultAsync(wmp => wmp.Id == id && wmp.UserId == userId);

                if (mealPlan == null)
                {
                    return false;
                }

                _context.MealSlots.RemoveRange(mealPlan.MealSlots);
                _context.WeeklyMealPlans.Remove(mealPlan);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private HashSet<string> DistributeRestaurantMeals(int totalRestaurantMeals, string[] days, string[] mealTypes)
        {
            var restaurantSlots = new HashSet<string>();
            var availableSlots = new List<string>();

            foreach (var day in days)
            {
                foreach (var mealType in mealTypes)
                {
                    availableSlots.Add($"{day}_{mealType}");
                }
            }

            for (int i = 0; i < Math.Min(totalRestaurantMeals, availableSlots.Count); i++)
            {
                if (availableSlots.Count > 0)
                {
                    var randomIndex = _random.Next(availableSlots.Count);
                    var selectedSlot = availableSlots[randomIndex];
                    restaurantSlots.Add(selectedSlot);
                    availableSlots.RemoveAt(randomIndex);
                }
            }

            return restaurantSlots;
        }

        private DailyMacros CalculateMealMacros(DailyMacros dailyMacros, string mealType)
        {
            var distribution = _mealTypeDistribution.GetValueOrDefault(mealType, 0.25f);

            return new DailyMacros
            {
                Calories = dailyMacros.Calories * distribution,
                Protein = dailyMacros.Protein * distribution,
                Carbs = dailyMacros.Carbs * distribution,
                Fat = dailyMacros.Fat * distribution
            };
        }

        private async Task<Recipe> SelectRecipeAsync(string mealType, DailyMacros targetMacros, HashSet<int> excludeRecipeIds = null)
        {
            var query = _context.Recipes
                .Include(r => r.RecipeIngredients)
                .Where(r => r.RecipeStatus == "Accepted");

            if (excludeRecipeIds != null && excludeRecipeIds.Any())
            {
                query = query.Where(r => !excludeRecipeIds.Contains(r.Id));
            }

            if (!string.IsNullOrEmpty(mealType))
            {
                query = query.Where(r => r.Type.Contains(mealType));
            }

            var recipes = await query.ToListAsync();

            if (!recipes.Any())
            {
                recipes = await _context.Recipes
                    .Where(r => r.RecipeStatus == "Accepted")
                    .ToListAsync();

                if (excludeRecipeIds != null && excludeRecipeIds.Any())
                {
                    recipes = recipes.Where(r => !excludeRecipeIds.Contains(r.Id)).ToList();
                }
            }

            if (!recipes.Any()) return null;

            var scoredRecipes = recipes.Select(recipe => new
            {
                Recipe = recipe,
                Score = CalculateMacroMatchScore(recipe, targetMacros)
            })
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToList();

            var selectedRecipe = scoredRecipes[_random.Next(Math.Min(3, scoredRecipes.Count))].Recipe;

            return selectedRecipe;
        }

        private async Task<RestaurantMeal> SelectRestaurantMealAsync(string mealType, DailyMacros targetMacros, HashSet<int> excludeMealIds = null)
        {
            var query = _context.RestaurantMeals.AsQueryable();

            if (excludeMealIds != null && excludeMealIds.Any())
            {
                query = query.Where(rm => !excludeMealIds.Contains(rm.Id));
            }

            if (!string.IsNullOrEmpty(mealType))
            {
                query = query.Where(rm => rm.Type.Contains(mealType));
            }

            var restaurantMeals = await query.ToListAsync();

            if (!restaurantMeals.Any()) return null;

            var scoredMeals = restaurantMeals.Select(meal => new
            {
                Meal = meal,
                Score = CalculateMacroMatchScore(meal, targetMacros)
            })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();

            return scoredMeals[_random.Next(scoredMeals.Count)].Meal;
        }

        private double CalculateMacroMatchScore(Recipe recipe, DailyMacros targetMacros)
        {
            var calorieMatch = 1.0 - Math.Abs(recipe.Calories - targetMacros.Calories) / targetMacros.Calories;
            var proteinMatch = 1.0 - Math.Abs(recipe.Protein - targetMacros.Protein) / Math.Max(targetMacros.Protein, 1);
            var carbMatch = 1.0 - Math.Abs(recipe.Carbs - targetMacros.Carbs) / Math.Max(targetMacros.Carbs, 1);
            var fatMatch = 1.0 - Math.Abs(recipe.Fat - targetMacros.Fat) / Math.Max(targetMacros.Fat, 1);

            return (calorieMatch * 0.4 + proteinMatch * 0.2 + carbMatch * 0.2 + fatMatch * 0.2) * 100;
        }

        private double CalculateMacroMatchScore(RestaurantMeal meal, DailyMacros targetMacros)
        {
            var calorieMatch = 1.0 - Math.Abs(meal.Calories - targetMacros.Calories) / targetMacros.Calories;
            var proteinMatch = 1.0 - Math.Abs(meal.Protein - targetMacros.Protein) / Math.Max(targetMacros.Protein, 1);
            var carbMatch = 1.0 - Math.Abs(meal.Carbs - targetMacros.Carbs) / Math.Max(targetMacros.Carbs, 1);
            var fatMatch = 1.0 - Math.Abs(meal.Fat - targetMacros.Fat) / Math.Max(targetMacros.Fat, 1);

            return (calorieMatch * 0.4 + proteinMatch * 0.2 + carbMatch * 0.2 + fatMatch * 0.2) * 100;
        }

        private float CalculateRemainingCalories(List<MealSlot> dayMeals, float targetCalories)
        {
            var totalCalories = dayMeals.Sum(ms =>
                ms.IsRestaurantMeal ? (ms.RestaurantMeal?.Calories ?? 0) : (ms.Recipe?.Calories ?? 0));

            return Math.Max(0, targetCalories - totalCalories);
        }

        private Dictionary<string, DailyMacros> CalculateDailyMacroTotals(WeeklyMealPlan weeklyPlan)
        {
            var dailyTotals = new Dictionary<string, DailyMacros>();
            var days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            foreach (var day in days)
            {
                var dayMeals = weeklyPlan.MealSlots.Where(ms => ms.Day == day).ToList();

                dailyTotals[day] = new DailyMacros
                {
                    Calories = dayMeals.Sum(ms => ms.IsRestaurantMeal ? (ms.RestaurantMeal?.Calories ?? 0) : (ms.Recipe?.Calories ?? 0)),
                    Protein = dayMeals.Sum(ms => ms.IsRestaurantMeal ? (ms.RestaurantMeal?.Protein ?? 0) : (ms.Recipe?.Protein ?? 0)),
                    Carbs = dayMeals.Sum(ms => ms.IsRestaurantMeal ? (ms.RestaurantMeal?.Carbs ?? 0) : (ms.Recipe?.Carbs ?? 0)),
                    Fat = dayMeals.Sum(ms => ms.IsRestaurantMeal ? (ms.RestaurantMeal?.Fat ?? 0) : (ms.Recipe?.Fat ?? 0))
                };
            }

            return dailyTotals;
        }

        public async Task<WeeklyMealPlan> GetMealPlanByIdAsync(int id, string userId)
        {
#pragma warning disable CS8603
            return await _context.WeeklyMealPlans
                .Include(wmp => wmp.MealSlots)
                    .ThenInclude(ms => ms.Recipe)
                        .ThenInclude(r => r.RecipeIngredients)
                .Include(wmp => wmp.MealSlots)
                    .ThenInclude(ms => ms.RestaurantMeal)
                        .ThenInclude(rm => rm.Restaurant)
                .FirstOrDefaultAsync(wmp => wmp.Id == id && wmp.UserId == userId);
        }

        public async Task<List<WeeklyMealPlan>> GetUserMealPlansAsync(string userId)
        {
            return await _context.WeeklyMealPlans
                .Where(wmp => wmp.UserId == userId)
                .Include(wmp => wmp.MealSlots)
                .OrderByDescending(wmp => wmp.GeneratedAt)
                .ToListAsync();
        }

        public async Task<bool> RegenerateMealSlotAsync(int mealSlotId, string userId)
        {
            try
            {
                var mealSlot = await _context.MealSlots
                    .Include(ms => ms.Recipe)
                    .Include(ms => ms.RestaurantMeal)
                    .FirstOrDefaultAsync(ms => ms.Id == mealSlotId);

                if (mealSlot == null)
                    return false;

                var weeklyPlan = await _context.WeeklyMealPlans
                    .FirstOrDefaultAsync(wmp => wmp.UserId == userId && wmp.MealSlots.Any(ms => ms.Id == mealSlotId));

                if (weeklyPlan == null)
                    return false;

                var currentRecipeId = mealSlot.Recipe?.Id;
                var currentRestaurantMealId = mealSlot.RestaurantMeal?.Id;

                var targetMacros = new DailyMacros
                {
                    Calories = mealSlot.IsRestaurantMeal ? (mealSlot.RestaurantMeal?.Calories ?? 500) : (mealSlot.Recipe?.Calories ?? 500),
                    Protein = mealSlot.IsRestaurantMeal ? (mealSlot.RestaurantMeal?.Protein ?? 30) : (mealSlot.Recipe?.Protein ?? 30),
                    Carbs = mealSlot.IsRestaurantMeal ? (mealSlot.RestaurantMeal?.Carbs ?? 50) : (mealSlot.Recipe?.Carbs ?? 50),
                    Fat = mealSlot.IsRestaurantMeal ? (mealSlot.RestaurantMeal?.Fat ?? 15) : (mealSlot.Recipe?.Fat ?? 15)
                };

                if (mealSlot.IsRestaurantMeal)
                {
                    var newRestaurantMeal = await SelectBestRestaurantMealAsync(mealSlot.MealType, targetMacros, currentRestaurantMealId);
                    if (newRestaurantMeal != null)
                    {
                        mealSlot.RestaurantMeal = newRestaurantMeal;
                        mealSlot.Recipe = null;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    var newRecipe = await SelectBestRecipeAsync(mealSlot.MealType, targetMacros, currentRecipeId);
                    if (newRecipe != null)
                    {
                        mealSlot.Recipe = newRecipe;
                        mealSlot.RestaurantMeal = null;
                    }
                    else
                    {
                        return false;
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<Recipe> SelectBestRecipeAsync(string mealType, DailyMacros targetMacros, int? excludeRecipeId = null)
        {
            var query = _context.Recipes
                .Include(r => r.RecipeIngredients)
                .Where(r => r.RecipeStatus == "Accepted");

            if (excludeRecipeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeRecipeId.Value);
            }

            if (!string.IsNullOrEmpty(mealType))
            {
                query = query.Where(r => r.Type.Contains(mealType));
            }

            var recipes = await query.ToListAsync();

            if (!recipes.Any())
            {
                recipes = await _context.Recipes
                    .Where(r => r.RecipeStatus == "Accepted" && r.Id != excludeRecipeId)
                    .ToListAsync();
            }

            if (!recipes.Any()) return null;

            var closestRecipe = recipes
                .Select(recipe => new
                {
                    Recipe = recipe,
                    Score = CalculateMacroMatchScore(recipe, targetMacros)
                })
                .OrderByDescending(x => x.Score)
                .First()
                .Recipe;

            return closestRecipe;
        }

        private async Task<RestaurantMeal> SelectBestRestaurantMealAsync(string mealType, DailyMacros targetMacros, int? excludeMealId = null)
        {
            var query = _context.RestaurantMeals.AsQueryable();

            if (excludeMealId.HasValue)
            {
                query = query.Where(rm => rm.Id != excludeMealId.Value);
            }

            if (!string.IsNullOrEmpty(mealType))
            {
                query = query.Where(rm => rm.Type.Contains(mealType));
            }

            var restaurantMeals = await query.ToListAsync();

            if (!restaurantMeals.Any())
            {
                restaurantMeals = await _context.RestaurantMeals
                    .Where(rm => rm.Id != excludeMealId)
                    .ToListAsync();
            }

            if (!restaurantMeals.Any()) return null;

            var closestMeal = restaurantMeals
                .Select(meal => new
                {
                    Meal = meal,
                    Score = CalculateMacroMatchScore(meal, targetMacros)
                })
                .OrderByDescending(x => x.Score)
                .First()
                .Meal;

            return closestMeal;
        }

        public async Task HandleDeletedRecipeAsync(int recipeId)
        {
            try
            {
                var affectedMealSlots = await _context.MealSlots
                    .Include(ms => ms.Recipe)
                    .Where(ms => ms.Recipe != null && ms.Recipe.Id == recipeId && !ms.IsRestaurantMeal)
                    .ToListAsync();

                if (!affectedMealSlots.Any())
                    return;

                var affectedUserIds = await _context.WeeklyMealPlans
                    .Where(wmp => wmp.MealSlots.Any(ms => affectedMealSlots.Select(ams => ams.Id).Contains(ms.Id)))
                    .Select(wmp => wmp.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var mealSlot in affectedMealSlots)
                {
                    var targetMacros = new DailyMacros
                    {
                        Calories = mealSlot.Recipe?.Calories ?? 500,
                        Protein = mealSlot.Recipe?.Protein ?? 30,
                        Carbs = mealSlot.Recipe?.Carbs ?? 50,
                        Fat = mealSlot.Recipe?.Fat ?? 15
                    };

                    var replacementRecipe = await SelectBestRecipeAsync(mealSlot.MealType, targetMacros, recipeId);

                    if (replacementRecipe != null)
                    {
                        mealSlot.Recipe = replacementRecipe;
                        mealSlot.IsRegenerated = true;
                        mealSlot.isViewed = false;
                    }
                    else
                    {
                        mealSlot.Recipe = null;
                    }
                }

                await _context.SaveChangesAsync();

                foreach (var userId in affectedUserIds)
                {
                    var UserNotification = await _context.Users.FindAsync(userId);
                    if (UserNotification.NotifyMealPlanUpdated)
                    {

                        var notification = new Notification
                        {
                            UserId = userId,
                            Type = "MealPlanUpdated",
                            Message = $"A recipe in your meal plan was removed and has been automatically replaced with a similar recipe.",
                            CreatedAt = DateTime.Now.ToUniversalTime(),

                            IsRead = false
                        };

                        _context.Notifications.Add(notification);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
            }
        }

        public async Task HandleDeletedRestaurantMealAsync(int restaurantMealId)
        {
            try
            {
                var affectedMealSlots = await _context.MealSlots
                    .Include(ms => ms.RestaurantMeal)
                    .Where(ms => ms.RestaurantMeal != null && ms.RestaurantMeal.Id == restaurantMealId && ms.IsRestaurantMeal)
                    .ToListAsync();

                if (!affectedMealSlots.Any())
                    return;

                var affectedUserIds = await _context.WeeklyMealPlans
                    .Where(wmp => wmp.MealSlots.Any(ms => affectedMealSlots.Select(ams => ams.Id).Contains(ms.Id)))
                    .Select(wmp => wmp.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var mealSlot in affectedMealSlots)
                {
                    var targetMacros = new DailyMacros
                    {
                        Calories = mealSlot.RestaurantMeal?.Calories ?? 500,
                        Protein = mealSlot.RestaurantMeal?.Protein ?? 30,
                        Carbs = mealSlot.RestaurantMeal?.Carbs ?? 50,
                        Fat = mealSlot.RestaurantMeal?.Fat ?? 15
                    };

                    var replacementMeal = await SelectBestRestaurantMealAsync(mealSlot.MealType, targetMacros, restaurantMealId);

                    if (replacementMeal != null)
                    {
                        mealSlot.RestaurantMeal = replacementMeal;
                        mealSlot.IsRegenerated = true;
                        mealSlot.isViewed = false;
                    }
                    else
                    {
                        mealSlot.RestaurantMeal = null;
                    }
                }

                await _context.SaveChangesAsync();

                foreach (var userId in affectedUserIds)
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        Type = "MealPlanUpdated",
                        Message = $"A restaurant meal in your meal plan was removed and has been automatically replaced with a similar restaurant meal.",
                        CreatedAt = DateTime.Now.ToUniversalTime(),
                        IsRead = false
                    };

                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
            }
        }
    }
}