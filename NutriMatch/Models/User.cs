using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace NutriMatch.Models
{
    public class User : IdentityUser
    {
        public virtual ICollection<Recipe> Recipes { get; set; }

        public String ProfilePictureUrl { get; set; }
        public ICollection<FavoriteRecipe> FavoriteRecipes { get; set; }
        public ICollection<RecipeRating> Ratings { get; set; }
        public ICollection<UserMealPreference> MealTagPreferences { get; set; }
        public ICollection<RestaurantFollowing> FollowedRestaurants { get; set; }

        public bool NotifyRecipeRated { get; set; } = true;
        public bool NotifyRecipeAccepted { get; set; } = true;
        public bool NotifyRecipeDeclined { get; set; } = true;
        public bool NotifyRestaurantNewMeal { get; set; } = true;
        public bool NotifyMealMatchesTags { get; set; } = true;
        public bool NotifyRecipeMatchesTags { get; set; } = true;
        public bool NotifyNewRestaurant { get; set; } = true;
        public bool NotifyMealPlanUpdated { get; set; } = true;
    }
}