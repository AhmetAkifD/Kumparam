using System;

namespace Kumparam.Core.Models
{
    public class GoalAutomation
    {
        public Guid AutomationId { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public Guid GoalId { get; set; }
        public decimal Amount { get; set; }
        public string Frequency { get; set; } // "Daily", "Weekly", "Monthly"
        public DateTime NextRunDate { get; set; }
        public bool IsActive { get; set; } = true;

        // Hedef ismini UI'da göstermek gerekirse diye eklendi
        public string? GoalTitle { get; set; }
    }
}