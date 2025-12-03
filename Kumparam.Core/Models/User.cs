namespace Kumparam.Core.Models
{
    // Bu sınıf, dbo.Users tablosundaki verileri temsil edecek
    // Sadece C# kodunda kullanacağımız bir model
    public class User
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
        public bool IsAdmin { get; set; }
    }
}