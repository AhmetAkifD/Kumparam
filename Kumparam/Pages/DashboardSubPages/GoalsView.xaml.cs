using System;
using System.Configuration;
using System.Windows.Controls;
using System.Text; // StringBuilder için gerekli
using Kumparam.Core;
using Kumparam.Data;

namespace Kumparam.Pages.DashboardSubPages;

public partial class GoalsView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    public GoalsView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Testi Başlat
        RunListTest();
    }

    public GoalsView()
    {
        InitializeComponent();
    }

    private void RunListTest()
    {
        try
        {
            // 1. Veritabanından listeyi çekmeyi dene
            var goals = _userRepository.GetGoals(_currentUserId);

            // 2. Sonuçları metne dök
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Sorgu Başarılı!");
            sb.AppendLine($"Bulunan Hedef Sayısı: {goals.Count}");
            sb.AppendLine("--------------------------------------------------");

            if (goals.Count == 0)
            {
                sb.AppendLine("HİÇ VERİ YOK. (Veritabanı boş veya UserId eşleşmiyor)");
            }
            else
            {
                foreach (var goal in goals)
                {
                    sb.AppendLine($"BAŞLIK: {goal.Title}");
                    sb.AppendLine($"TUTAR: {goal.TargetAmount}");
                    sb.AppendLine($"TARİH: {goal.Deadline}");
                    sb.AppendLine("---");
                }
            }

            // 3. Ekrana yaz
            DebugResultText.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            // Hata varsa hatayı yaz
            DebugResultText.Text = $"KRİTİK HATA OLUŞTU:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
    }
}