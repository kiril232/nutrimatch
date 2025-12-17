using NutriMatch.Data;
using NutriMatch.Models;
using Microsoft.EntityFrameworkCore;

namespace NutriMatch.Services
{
    public class MealKeywordService : IMealKeywordService
    {
        private readonly AppDbContext _context;

        public MealKeywordService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MealKeyword>> GetMealKeywordsAsync()
        {
            return await _context.MealKeywords
                .OrderBy(k => k.Tag)
                .ThenBy(k => k.Name)
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<string>>> GetKeywordsByTagAsync()
        {
            var keywords = await _context.MealKeywords.ToListAsync();
            
            return keywords
                .GroupBy(k => k.Tag.ToLower())
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(k => k.Name).ToList()
                );
        }

        public async Task<(bool success, string message)> AddMealKeywordAsync(MealKeyword keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword.Name))
            {
                return (false, "Keyword name is required");
            }

            var exists = await _context.MealKeywords
                .AnyAsync(k => k.Name.ToLower() == keyword.Name.ToLower() && k.Tag == keyword.Tag);

            if (exists)
            {
                return (false, "This keyword already exists for this meal type");
            }

            _context.MealKeywords.Add(keyword);
            await _context.SaveChangesAsync();

            return (true, "Keyword added successfully");
        }

        public async Task<(bool success, string message)> DeleteMealKeywordAsync(int id)
        {
            var keyword = await _context.MealKeywords.FindAsync(id);
            if (keyword == null)
            {
                return (false, "Keyword not found");
            }

            _context.MealKeywords.Remove(keyword);
            await _context.SaveChangesAsync();

            return (true, "Keyword deleted successfully");
        }
    }
}