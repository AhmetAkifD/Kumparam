using System;

namespace Kumparam.Core.Models
{
    public class AutoTransaction
    {
        public Guid AutoId { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } // "Income" veya "Expense"
        public string Category { get; set; }
        public string Description { get; set; }
        public string Frequency { get; set; } // "Daily", "Weekly", "Monthly"
        public DateTime NextRunDate { get; set; }
        public bool IsActive { get; set; } = true;

        // UI tarafında kolaylık sağlaması için yardımcı özellik
        public string FrequencyText => Frequency switch
        {
            "Daily" => "Günlük",
            "Weekly" => "Haftalık",
            "Monthly" => "Aylık",
            _ => "Bilinmiyor"
        };
    }
}