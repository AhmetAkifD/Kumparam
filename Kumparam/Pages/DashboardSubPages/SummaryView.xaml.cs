using System.Windows.Controls;
using System.Configuration;
using Kumparam.Core;
using Kumparam.Data;

namespace Kumparam.Pages.DashboardSubPages;

public partial class SummaryView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId; // Giriş yapan kullanıcının ID'si lazım

    // Parametresiz constructor (XAML editörü için gerekebilir ama biz alttakini kullanacağız)
    public SummaryView()
    {
        InitializeComponent();
    }

    // Kullanıcı ID'si alan constructor
    public SummaryView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;

        // Repository'i başlat
        string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
        _userRepository = new SqlUserRepository(connectionString);

        // Verileri Yükle
        LoadData();
    }

    private void LoadData()
    {
        // Veritabanından özeti çek
        var summary = _userRepository.GetFinancialSummary(_currentUserId);

        // Ekrana yazdır (XAML'deki TextBlock'lara x:Name vermemiz gerekecek!)
        // Şimdilik örnek olarak:
        // TotalBalanceText.Text = $"₺ {summary.TotalBalance:N2}";
    }
}