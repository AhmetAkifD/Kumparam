using System;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages;

public partial class RecycleBinView : UserControl
{
    private readonly IUserRepository _userRepository;
    private readonly Guid _currentUserId;

    public RecycleBinView(Guid userId)
    {
        InitializeComponent();
        _currentUserId = userId;
        _userRepository = new SqlUserRepository(ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString);
        
        LoadDeletedItems();
    }

    private void LoadDeletedItems()
    {
        try
        {
            var items = _userRepository.GetDeletedTransactions(_currentUserId);
            
            // Description içinde gizlediğimiz ID'yi burada ayıklamıyoruz, 
            // sadece kullanıcıya gösterirken temizlemek isteyebiliriz ama şimdilik kalsın.
            // Önemli olan butona basınca ID'yi alabilmek.
            
            DeletedGrid.ItemsSource = items;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Yükleme hatası: " + ex.Message);
        }
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        // Butonun Tag'inde "Açıklama || 5" gibi bir string var. Oradan ID'yi çekeceğiz.
        if (sender is Button btn && btn.Tag is string rawData)
        {
            try
            {
                // String parse işlemi (Hack çözümü)
                var parts = rawData.Split(new[] { " || " }, StringSplitOptions.None);
                if (parts.Length < 2) return;

                if (int.TryParse(parts.Last(), out int deletedId))
                {
                    if (MessageBox.Show("Bu işlemi geri yüklemek istiyor musunuz?", "Geri Yükle", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        _userRepository.RestoreTransaction(deletedId);
                        MessageBox.Show("İşlem başarıyla geri yüklendi! ✅");
                        LoadDeletedItems(); // Listeyi yenile
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message);
            }
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        // Profil Sayfasına Geri Dön
        var dashboard = Window.GetWindow(this) as DashboardWindow;
        if (dashboard != null)
        {
            dashboard.MainContentArea.Content = new ProfileView(); 
        }
    }
}