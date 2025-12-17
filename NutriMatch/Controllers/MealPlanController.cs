using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NutriMatch.Data;
using NutriMatch.Models;
using NutriMatch.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NutriMatch.Controllers
{
     [Authorize]
    public class MealPlanController : Controller
    {
        private readonly IMealPlanService _mealPlanService;
        private readonly UserManager<User> _userManager;
        private readonly AppDbContext _context;

        public MealPlanController(IMealPlanService mealPlanService, UserManager<User> userManager, AppDbContext context)
        {
            _mealPlanService = mealPlanService;
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MealPlanRequest model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _mealPlanService.GenerateWeeklyMealPlanAsync(user.Id, model);

            if (result.Success)
            {
                TempData["Success"] = "Weekly meal plan generated successfully!";
                return RedirectToAction("Details", new { id = result.WeeklyMealPlan.Id });
            }
            else
            {
                ModelState.AddModelError("", result.ErrorMessage ?? "Failed to generate meal plan. Please try again.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var mealPlan = await _mealPlanService.GetMealPlanByIdAsync(id, user.Id);
            if (mealPlan == null)
            {
                TempData["Error"] = "Meal plan not found or you don't have access to it.";
                return RedirectToAction("Index");
            }

            return View(mealPlan);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var mealPlans = await _mealPlanService.GetUserMealPlansAsync(user.Id);
            return View(mealPlans);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var result = await _mealPlanService.DeleteMealPlanAsync(id, user.Id);

            if (result)
            {
                TempData["Success"] = "Meal plan deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to delete meal plan. It may not exist or you don't have permission to delete it.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateMeal(int mealSlotId, int mealPlanId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            var result = await _mealPlanService.RegenerateMealSlotAsync(mealSlotId, user.Id);

            if (result)
            {
                return Json(new { success = true, message = "Meal regenerated successfully!" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to regenerate meal. Please try again." });
            }
        }


        [HttpPost]
        public async Task<JsonResult> MarkMealsAsViewed([FromQuery] int mealId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var mealSlot = await _context.MealSlots
                    .FirstOrDefaultAsync(ms => ms.Id == mealId);

                if (mealSlot == null)
                {
                    return Json(new { success = false, message = "Meal not found" });
                }

                if (mealSlot.IsRegenerated == true && mealSlot.isViewed == false)
                {
                    mealSlot.isViewed = true;
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Meal marked as viewed" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MarkMealsAsViewed: {ex.Message}");
                return Json(new { success = false, message = $"Error marking meal as viewed: {ex.Message}" });
            }
        }





    }
}