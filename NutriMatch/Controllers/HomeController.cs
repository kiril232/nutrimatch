using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;
using Microsoft.AspNetCore.Identity;

namespace MyApp.Namespace
{
    public class Home : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;

        public Home(AppDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var recipes = await _context.Recipes
                .Where(r => r.RecipeStatus == "Accepted")
                .Include(r => r.User)
                .Include(r => r.Ratings)
                .Select(r => new
                {
                    Recipe = r,
                    AverageRating = r.Ratings.Any() ? r.Ratings.Average(rating => rating.Rating) : 0
                })
                .OrderByDescending(x => x.AverageRating)
                .Take(6)
                .Select(x => x.Recipe)
                .ToListAsync();

            foreach (var recipe in recipes)
            {
                recipe.Rating = recipe.Ratings.Any() ? recipe.Ratings.Average(r => r.Rating) : 0;
            }

            var topRestaurants = await _context.Restaurants
                .Include(r => r.Followers)
                .OrderByDescending(r => r.Followers.Count)
                .Take(5)
                .ToListAsync();

            var model = new HomeViewModel
            {
                Recipes = recipes,
                Restaurants = topRestaurants
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRecipes = _context.Recipes
                .Where(r => r.UserId == userId)
                .Include(r => r.User)
                .Include(r => r.Ratings)
                .ToList();

            var recipeIds = userRecipes.Select(r => r.Id).ToList();
            var ratings = _context.RecipeRatings
                .Where(r => recipeIds.Contains(r.RecipeId))
                .GroupBy(r => r.RecipeId);

            foreach (var recipe in userRecipes)
            {
                recipe.Rating = recipe.Ratings.Any() ? recipe.Ratings.Average(r => r.Rating) : 0;
            }

            double averageRating = 0;
            foreach (var group in ratings)
            {
                averageRating += group.Average(r => r.Rating);
            }

            if (ratings.Count() > 0)
            {
                ViewBag.AverageRating = Math.Round(averageRating / ratings.Count(), 1);
            }
            else
            {
                ViewBag.AverageRating = 0;
            }

            ViewBag.UserRecipesCount = userRecipes.Count;

            if (User.Identity.IsAuthenticated && !string.IsNullOrEmpty(userId))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.UserPicture = currentUser?.ProfilePictureUrl;
            }

            return View(model);
        }
    }
}