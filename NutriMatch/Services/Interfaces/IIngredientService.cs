using System.Collections.Generic;
using System.Threading.Tasks;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IIngredientService
    {
        Task<List<Ingredient>> GetIngredientSuggestionsAsync(string query);
        Task<(bool success, string message, Ingredient ingredient)> AddIngredientAsync(string name, float calories, float protein, float carbs, float fat);
    }
}
