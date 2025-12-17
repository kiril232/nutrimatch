using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Controllers;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class MealClassificationService : IMealClassificationService
    {

        private readonly IMealKeywordService _mealKeywordService;
        private readonly AppDbContext _context;

        public MealClassificationService(AppDbContext context, IMealKeywordService mealKeywordService)
        {
            _context = context;
            _mealKeywordService = mealKeywordService;
        }

       

        public async Task<List<string>> GenerateMealTypes(RestaurantMeal meal)
        {
            if (meal.Calories == 0 ||
                (!string.IsNullOrEmpty(meal.ItemDescription) &&
                (meal.ItemDescription.ToLower().Contains("wine") ||
                meal.ItemDescription.ToLower().Contains("beer") ||
                meal.ItemDescription.ToLower().Contains("spirits") ||
                meal.ItemDescription.ToLower().Contains("beverages"))))
            {
                return new List<string> { "drink" };
            }

            var keywords = await _mealKeywordService.GetKeywordsByTagAsync();
            var tags = new HashSet<string>();

            var breakfastKeywords = new HashSet<string>(
                keywords.ContainsKey("breakfast") ? keywords["breakfast"] : new List<string>(), 
                StringComparer.OrdinalIgnoreCase);
            var mainKeywords = new HashSet<string>(
                keywords.ContainsKey("main") ? keywords["main"] : new List<string>(), 
                StringComparer.OrdinalIgnoreCase);
            var snackKeywords = new HashSet<string>(
                keywords.ContainsKey("snack") ? keywords["snack"] : new List<string>(), 
                StringComparer.OrdinalIgnoreCase);

            var titleWords = meal.ItemName.ToLower()
                .Split(new char[] { ' ', '-', '_', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            var descriptionWords = new HashSet<string>();
            if (!string.IsNullOrEmpty(meal.ItemDescription))
            {
                var words = meal.ItemDescription.ToLower()
                    .Split(new char[] { ' ', '-', '_', ',', '.', '(', ')', ';', ':' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words) descriptionWords.Add(w);
            }

            int breakfastScore = CountKeywordMatches(titleWords, breakfastKeywords, true) +
                                CountKeywordMatches(descriptionWords, breakfastKeywords, false);

            int mainScore = CountKeywordMatches(titleWords, mainKeywords, true) +
                            CountKeywordMatches(descriptionWords, mainKeywords, false);

            int snackScore = CountKeywordMatches(titleWords, snackKeywords, true) +
                            CountKeywordMatches(descriptionWords, snackKeywords, false);

            int lunchScore = mainScore;
            int dinnerScore = mainScore;

            float calories = meal.Calories;
            float proteinRatio = (meal.Protein * 4) / calories * 100;
            float carbRatio = (meal.Carbs * 4) / calories * 100;
            float fatRatio = (meal.Fat * 9) / calories * 100;

            if (calories < 250)
            {
                snackScore += 2;
                breakfastScore += 1;
                dinnerScore -= 2;
                lunchScore -= 2;
            }
            else if (calories <= 500)
            {
                lunchScore += 1;
                dinnerScore += 1;
                breakfastScore += 2;
            }
            else
            {
                dinnerScore += 2;
                lunchScore += 2;
                breakfastScore -= 1;
                snackScore -= 2;
            }

            if (proteinRatio >= 25)
            {
                dinnerScore += 2;
                lunchScore += 2;
            }
            else if (carbRatio >= 50)
            {
                breakfastScore += 1;
                snackScore += 1;
            }

            if (fatRatio > 30)
            {
                dinnerScore += 1;
                snackScore += 1;
            }

            var results = new List<(string tag, int score)>
            {
                ("breakfast", breakfastScore),
                ("lunch", lunchScore),
                ("dinner", dinnerScore),
                ("snack", snackScore)
            }.OrderByDescending(x => x.score).ToList();

            tags.Add(results[0].tag);

            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].score > 0 && results[i].score >= results[0].score * 0.6)
                    tags.Add(results[i].tag);
            }

            return tags.ToList();
        }

        private string NormalizeWord(string word)
        {
            word = word.ToLower().Trim();
            if (word.EndsWith("ies") && word.Length > 4)
                return word.Substring(0, word.Length - 3) + "y";
            if (word.EndsWith("es") && word.Length > 3)
                return word.Substring(0, word.Length - 2);
            if (word.EndsWith("s") && word.Length > 3 && !word.EndsWith("ss"))
                return word.Substring(0, word.Length - 1);
            return word;
        }

        private int CountKeywordMatches(IEnumerable<string> words, HashSet<string> keywords, bool isTitle = false)
        {
            int count = 0;
            foreach (var word in words)
            {
                bool matches = keywords.Contains(word) || keywords.Contains(NormalizeWord(word));
                if (matches)
                    count += isTitle ? 3 : 1;
            }
            return count;
        }

        private async Task<List<RestaurantMeal>> GetUnclassifiedMealsFromDatabaseAsync()
        {
            return await _context.RestaurantMeals.ToListAsync();
        }

        private async Task UpdateMealTypesInDatabaseAsync(int mealId, List<string> mealTypes)
        {
            var meal = await _context.RestaurantMeals.FindAsync(mealId);
            if (meal != null)
            {
                meal.Type = mealTypes;
                await _context.SaveChangesAsync();
            }
        }
    }
}