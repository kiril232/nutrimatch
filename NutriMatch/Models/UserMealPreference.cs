
namespace NutriMatch.Models
{
    public class UserMealPreference
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public virtual User User { get; set; }
        public string Tag { get; set; }
        public int? ThresholdValue { get; set; }
    }
}