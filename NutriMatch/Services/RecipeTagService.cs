using System;
using System.Collections.Generic;
using System.Linq;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class RecipeTagService : IRecipeTagService
    {
        private readonly AppDbContext _context;

        public RecipeTagService(AppDbContext context)
        {
            _context = context;
        }

        public List<string> GenerateRecipeTags(Recipe recipe, List<SelectedIngredient> ingredients)
        {
            var breakfastKeywords = new HashSet<string>(
                _context.MealKeywords.Where(k => k.Tag == "breakfast").Select(k => k.Name),
                StringComparer.OrdinalIgnoreCase
            );

            var mainKeywords = new HashSet<string>(
                _context.MealKeywords.Where(k => k.Tag == "main").Select(k => k.Name),
                StringComparer.OrdinalIgnoreCase
            );

            var snackKeywords = new HashSet<string>(
                _context.MealKeywords.Where(k => k.Tag == "snack").Select(k => k.Name),
                StringComparer.OrdinalIgnoreCase
            );

            var tags = new HashSet<string>();

            var titleWords = recipe.Title.ToLower()
                .Split(new char[] { ' ', '-', '_', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            var ingredientWords = new HashSet<string>();
            foreach (var ing in ingredients)
            {
                var words = ing.Name.ToLower()
                    .Split(new char[] { ' ', '-', '_', ',', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var w in words) ingredientWords.Add(w);
            }

            int breakfastScore = CountKeywordMatches(titleWords, breakfastKeywords, true) +
                                CountKeywordMatches(ingredientWords, breakfastKeywords, false);

            int mainScore = CountKeywordMatches(titleWords, mainKeywords, true) +
                            CountKeywordMatches(ingredientWords, mainKeywords, false);

            int snackScore = CountKeywordMatches(titleWords, snackKeywords, true) +
                            CountKeywordMatches(ingredientWords, snackKeywords, false);

            int lunchScore = mainScore;
            int dinnerScore = mainScore;

            float calories = Math.Max(recipe.Calories, 1);
            float proteinRatio = (recipe.Protein * 4) / calories * 100;
            float carbRatio = (recipe.Carbs * 4) / calories * 100;
            float fatRatio = (recipe.Fat * 9) / calories * 100;

            if (calories < 150)
            {
                snackScore += 5;
                breakfastScore -= 2;
                lunchScore -= 3;
                dinnerScore -= 4;
            }
            else if (calories < 300)
            {
                snackScore += 3;
                breakfastScore += 2;
                lunchScore -= 1;
                dinnerScore -= 2;
            }
            else if (calories < 450)
            {
                breakfastScore += 3;
                lunchScore += 2;
                snackScore -= 1;
                dinnerScore -= 1;
            }
            else if (calories < 650)
            {
                lunchScore += 3;
                dinnerScore += 2;
                breakfastScore -= 1;
                snackScore -= 3;
            }
            else
            {
                dinnerScore += 4;
                lunchScore += 1;
                breakfastScore -= 3;
                snackScore -= 4;
            }

            // Protein ratio scoring
            if (proteinRatio > 30)
            {
                dinnerScore += 3;
                lunchScore += 2;
                breakfastScore += 1;
                snackScore -= 1;
            }
            else if (proteinRatio > 20)
            {
                dinnerScore += 2;
                lunchScore += 1;
            }
            else if (proteinRatio < 10)
            {
                snackScore += 2;
                dinnerScore -= 1;
                lunchScore -= 1;
            }

            // Carb ratio scoring
            if (carbRatio > 60)
            {
                breakfastScore += 2;
                snackScore += 2;
                dinnerScore -= 1;
            }
            else if (carbRatio < 20)
            {
                dinnerScore += 1;
                lunchScore += 1;
            }

            // Fat ratio scoring
            if (fatRatio > 40)
            {
                dinnerScore += 2;
                snackScore += 1;
                breakfastScore -= 1;
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
    }
}