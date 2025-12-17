using Microsoft.EntityFrameworkCore;
using NutriMatch.Models;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;


namespace NutriMatch.Data
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

        public DbSet<Ingredient> Ingredients { get; set; }

        public DbSet<RestaurantMeal> RestaurantMeals { get; set; }

        public DbSet<Restaurant> Restaurants { get; set; }

        public DbSet<FavoriteRecipe> FavoriteRecipes { get; set; }

        public DbSet<RecipeRating> RecipeRatings { get; set; }
        public DbSet<WeeklyMealPlan> WeeklyMealPlans { get; set; }
        public DbSet<MealSlot> MealSlots { get; set; }
        
        public DbSet<MealKeyword> MealKeywords { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<UserMealPreference> UserMealPreferences { get; set; }
        public DbSet<RestaurantFollowing> RestaurantFollowings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.User)
                .WithMany(u => u.Recipes)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FavoriteRecipe>()
            .HasKey(fr => new { fr.UserId, fr.RecipeId });

            modelBuilder.Entity<FavoriteRecipe>()
                .HasOne(fr => fr.User)
                .WithMany(u => u.FavoriteRecipes)
                .HasForeignKey(fr => fr.UserId);

            modelBuilder.Entity<FavoriteRecipe>()
                .HasOne(fr => fr.Recipe)
                .WithMany(r => r.FavoritedBy)
                .HasForeignKey(fr => fr.RecipeId);

            modelBuilder.Entity<RecipeRating>()
            .HasIndex(rr => new { rr.UserId, rr.RecipeId })
            .IsUnique();

            modelBuilder.Entity<RecipeRating>()
                .HasOne(rr => rr.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(rr => rr.UserId);

            modelBuilder.Entity<RecipeRating>()
                .HasOne(rr => rr.Recipe)
                .WithMany(r => r.Ratings)
                .HasForeignKey(rr => rr.RecipeId);

            modelBuilder.Entity<UserMealPreference>()
                .HasIndex(u => new { u.UserId, u.Tag })
                .IsUnique();
    
            modelBuilder.Entity<RestaurantFollowing>()
                .HasIndex(r => new { r.UserId, r.RestaurantId })
                .IsUnique();

        }
    }

   

}