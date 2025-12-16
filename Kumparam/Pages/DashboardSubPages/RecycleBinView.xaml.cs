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
            
            string connectionString = ConfigurationManager.ConnectionStrings["KumparamDB"].ConnectionString;
            _userRepository = new SqlUserRepository(connectionString);
            
            LoadDeletedItems();
        }

        private void LoadDeletedItems()
        {
            try
            {
                // Repository metodumuz artık sadece IsHidden=0 olanları getiriyor.
                var items = _userRepository.GetDeletedTransactions(_currentUserId);
                DeletedGrid.ItemsSource = items;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Yükleme hatası: " + ex.Message);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessTransactionAction(sender, "Restore");
        }

        private void PurgeButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessTransactionAction(sender, "Purge");
        }

        private void ProcessTransactionAction(object sender, string actionType)
        {
            if (sender is Button btn && btn.Tag is string rawData)
            {
                try
                {
                    var parts = rawData.Split(new[] { " || " }, StringSplitOptions.None);
                    if (parts.Length < 2) return;

                    if (int.TryParse(parts.Last(), out int deletedId))
                    {
                        if (actionType == "Restore")
                        {
                            if (MessageBox.Show("Bu işlemi geri yüklemek istiyor musunuz?", "Geri Yükle", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                            {
                                _userRepository.RestoreTransaction(deletedId);
                                MessageBox.Show("İşlem başarıyla geri yüklendi! ✅");
                                LoadDeletedItems();
                            }
                        }
                        else if (actionType == "Purge")
                        {
                            // Kullanıcıya "Kalıcı Sil" diyoruz (İllüzyon)
                            if (MessageBox.Show("Bu işlem geri alınamaz ve kayıt kalıcı olarak silinecektir.\nEmin misiniz?", "Kalıcı Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                // ARKA PLAN: Aslında sadece gizliyoruz (Soft Delete)
                                _userRepository.PermanentlyDeleteTransaction(deletedId);
                                
                                MessageBox.Show("İşlem kalıcı olarak silindi.", "Başarılı");
                                
                                // Listeyi yeniliyoruz (Artık IsHidden=1 olduğu için listede çıkmayacak)
                                LoadDeletedItems();
                            }
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
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow != null)
            {
                parentWindow.Close();
            }
        }
    }
}