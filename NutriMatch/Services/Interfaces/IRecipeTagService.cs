using System.Collections.Generic;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IRecipeTagService
    {
        List<string> GenerateRecipeTags(Recipe recipe, List<SelectedIngredient> ingredients);
    }
}