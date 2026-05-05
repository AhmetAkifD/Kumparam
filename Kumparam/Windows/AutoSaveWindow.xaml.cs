using System.Windows;
using System.Windows.Controls;

namespace Kumparam.Windows
{
    public partial class AutoSaveWindow : Window
    {
        public decimal Amount { get; private set; }
        public string Frequency { get; private set; }
        public bool IsCancelled { get; private set; }

        public AutoSaveWindow()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(AmountTextBox.Text, out decimal amount) && amount > 0)
            {
                Amount = amount;
                Frequency = ((ComboBoxItem)FrequencyComboBox.SelectedItem).Tag.ToString();
                IsCancelled = false;
                DialogResult = true; // Pencereyi "Başarılı" sinyaliyle kapat
            }
            else
            {
                MessageBox.Show("Lütfen geçerli bir tutar girin.");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Kullanıcı otomasyonu silmek istiyorsa burası çalışır
            IsCancelled = true;
            DialogResult = true;
        }
    }
}