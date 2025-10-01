using System;
using System.Collections.Generic;

namespace Reports.Models.ViewModels
{
    public class DashboardViewModel
    {
        // 📊 Bar Chart: Venue Breakdown
        public List<string> VenueLabels { get; set; } = new();
        public List<decimal> TotalMpesaCharges { get; set; } = new();
        public List<decimal> TotalFarmers { get; set; } = new();
        public List<decimal> TotalEA { get; set; } = new();

        // 📈 Line Chart: Hall Hire Trends
        public List<string> TrendMonths { get; set; } = new();
        public List<decimal> HallHireTotals { get; set; } = new();
        public decimal TotalHallHireSpending { get; set; }

        // 🍩 Doughnut Chart: Payment Status
        public int PaidCount { get; set; }
        public int UnpaidCount { get; set; }

        // 🥧 Pie Chart: Lead Farmers vs EA Spending
        public decimal TotalLeadFarmersSpending { get; set; }
        public decimal TotalEASpending { get; set; }
    }
}
