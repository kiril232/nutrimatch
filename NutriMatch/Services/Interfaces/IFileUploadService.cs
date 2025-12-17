using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NutriMatch.Services
{
    public interface IFileUploadService
    {
        Task<string> UploadImageAsync(IFormFile file);
        Task DeleteImageAsync(string imageUrl);
    }
}