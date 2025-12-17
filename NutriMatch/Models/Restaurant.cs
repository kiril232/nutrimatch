using System.ComponentModel.DataAnnotations;

namespace NutriMatch.Models
{
    public class Restaurant
    {
        [Key]
        public int Id { get; set; }

        public String Name { get; set; }

        public String ImageUrl { get; set; }

        public String Description {get; set; }

        virtual public List<RestaurantMeal> RestaurantMeals { get; set; }

        public virtual ICollection<RestaurantFollowing> Followers { get; set; }
    }
}