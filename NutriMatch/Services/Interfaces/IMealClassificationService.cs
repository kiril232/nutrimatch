using System.Collections.Generic;
using System.Threading.Tasks;
using NutriMatch.Models;

namespace NutriMatch.Services
{
    public interface IMealClassificationService
    {
        
        Task<List<string>> GenerateMealTypes(RestaurantMeal meal);
    }
}