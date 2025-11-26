namespace Kumparam.Core
{
    // Bu sınıf, dbo.UserProfiles tablosundaki verileri temsil edecek
    public class UserProfile
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }
}