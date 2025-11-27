using System;

namespace Kumparam.Core
{
    public class Transaction
    {
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty; // "Income" veya "Expense"
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
    }
}