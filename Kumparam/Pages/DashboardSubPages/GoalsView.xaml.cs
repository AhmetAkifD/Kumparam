using System;
using System.Configuration;
using System.Windows.Controls;
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
        RunSimpleTest();
    }

    // Boş constructor (Hata vermemesi için)
    public GoalsView()
    {
        InitializeComponent();
    }

    private void RunSimpleTest()
    {
        try
        {
            // Basitçe tek bir string çekiyoruz
            string title = _userRepository.GetFirstGoalTitle(_currentUserId);
            
            // Ekrana yazıyoruz
            TestTitleText.Text = title;
        }
        catch (Exception ex)
        {
            TestTitleText.Text = "HATA: " + ex.Message;
        }
    }
}