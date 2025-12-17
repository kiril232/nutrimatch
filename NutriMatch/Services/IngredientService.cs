using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NutriMatch.Data;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public class IngredientService : IIngredientService
    {
        private readonly AppDbContext _context;

        public IngredientService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ingredient>> GetIngredientSuggestionsAsync(string query)
        {
            return await _context.Ingredients
                .Where(i => EF.Functions.ILike(i.Name, $"%{query}%") && i.Status == null)
                .OrderBy(i => i.Name)
                .Take(5)
                .ToListAsync();
        }

        public async Task<(bool success, string message, Ingredient ingredient)> AddIngredientAsync(string name, float calories, float protein, float carbs, float fat)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return (false, "Ingredient name is required.", null);
            }

            var existingIngredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower());

            if (existingIngredient != null)
            {
                return (false, "An ingredient with this name already exists.", null);
            }

            var ingredient = new Ingredient
            {
                Name = name.Trim(),
                Calories = calories,
                Protein = protein,
                Carbs = carbs,
                Fat = fat,
                Status = "Pending"
            };

            _context.Ingredients.Add(ingredient);
            await _context.SaveChangesAsync();

            return (true, "Ingredient added successfully", ingredient);
        }
    }
}