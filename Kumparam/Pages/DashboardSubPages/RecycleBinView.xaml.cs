using System;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Kumparam.Core.Interfaces;
using Kumparam.Data.Repositories;

namespace Kumparam.Pages.DashboardSubPages
{
    public partial class RecycleBinView : UserControl
    {
        private readonly IUserRepository _userRepository;
        private readonly Guid _currentUserId;

        public RecycleBinView(Guid userId)
        {
            InitializeComponent();
            _currentUserId = userId;
            // Repository bağlantısı
            _userRepository = new SqlUserRepository(ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString);
            
            LoadDeletedItems();
        }

        private void LoadDeletedItems()
        {
            try
            {
                var items = _userRepository.GetDeletedTransactions(_currentUserId);
                
                // Description içinde gizlediğimiz ID burada.
                // Kullanıcıya gösterirken " || ID" kısmını temizlemek istersen burada bir ViewModel mapping yapabilirsin
                // ama şimdilik "Olduğu gibi" bırakıyoruz.
                
                DeletedGrid.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yükleme hatası: " + ex.Message);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            // Repository'deki "Description + ' || ' + DeletedId" mantığını çözümlüyoruz.
            if (sender is Button btn && btn.Tag is string rawData)
            {
                try
                {
                    // String parse işlemi (Senin hack yöntemin, aynen korundu)
                    var parts = rawData.Split(new[] { " || " }, StringSplitOptions.None);
                    
                    // Eğer format bozuksa işlem yapma
                    if (parts.Length < 2) return;

                    // Son parça ID olmalı
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
            // DEĞİŞİKLİK BURADA:
            // Artık Dashboard içindeki sayfayı değiştirmiyoruz.
            // Bu UserControl bir "Window" (Dialog) içinde açıldığı için, o pencereyi kapatıyoruz.
            
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
                // Pencere kapanınca, ProfileView.xaml.cs içindeki ShowDialog() satırından sonraki kod çalışır
                // ve LoadProfile() tetiklenir.
            }
        }
    }
}