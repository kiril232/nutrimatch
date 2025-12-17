using System.ComponentModel.DataAnnotations;

namespace NutriMatch.Models
{
    public class MealSlot
    {
        [Key]
        public int Id { get; set; }
        public string Day { get; set; }
        public string MealType { get; set; }
        public Recipe? Recipe { get; set; }
        public RestaurantMeal? RestaurantMeal { get; set; }
        public bool IsRestaurantMeal { get; set; }

        public bool? IsRegenerated { get; set; } = false;
        
        public bool? isViewed { get; set; } = false;
    }
}