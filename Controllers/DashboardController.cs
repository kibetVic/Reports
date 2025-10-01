using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using Reports.Data;
using Reports.Models;
using Reports.Models.ViewModels;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

public class DashboardController : Controller
{
    private readonly ReportsDbContext _context;

    public DashboardController(ReportsDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // ✅ Get all summaries once
        var summaries = await _context.Summaries
            .Include(s => s.Voucher)
            .Include(s => s.County)
            .ToListAsync();

        // ============================
        // 📊 1. Venue Breakdown (6 most recent)
        // ============================
        var recentVenueData = await _context.Summaries
            .OrderByDescending(s => s.Date)
            .GroupBy(s => s.Venue)
            .Take(6)
            .Select(g => new
            {
                Venue = g.Key,
                TotalMpesa = g.Sum(x =>
                    x.TotalMpesaChargesPerHall +
                    x.TotalMpesaChargesPerLeadFarmers +
                    x.TotalMpesaChargesPerEA),
                TotalFarmers = g.Sum(x => x.LeadFarmersTotal),
                TotalEA = g.Sum(x => x.EATotal)
            })
            .ToListAsync();

        // ============================
        // 📈 2. Monthly Hall Hire Trends
        // ============================
        var monthlyTrends = summaries
            .GroupBy(s => new { s.Date.Year, s.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                TotalHallHire = g.Sum(x => x.HallHireTotal)
            })
            .ToList();

        // ============================
        // 🍩 3. Paid vs Unpaid
        // ============================
        var paidCount = summaries.Count(s => s.status == PaymentStatus.Paid);
        var unpaidCount = summaries.Count(s => s.status == PaymentStatus.Unpaid);

        // ============================
        // 🥧 4. Lead Farmers vs EA vs Hall Hire Spending
        // ============================
        var totalLeadFarmersSpending = summaries.Sum(s => s.LeadFarmersTotal);
        var totalEASpending = summaries.Sum(s => s.EATotal);
        var totalHallHireSpending = summaries.Sum(s => s.HallHireTotal);

        // ============================
        // ✅ Build Dashboard ViewModel
        // ============================
        var model = new DashboardViewModel
        {
            // 📊 Recent Venue Breakdown
            VenueLabels = recentVenueData.Select(v => v.Venue).ToList(),
            TotalMpesaCharges = recentVenueData.Select(v => v.TotalMpesa).ToList(),
            TotalFarmers = recentVenueData.Select(v => v.TotalFarmers).ToList(),
            TotalEA = recentVenueData.Select(v => v.TotalEA).ToList(),

            // 📈 Monthly Trends
            TrendMonths = monthlyTrends.Select(m => m.Month.ToString("MMM yyyy")).ToList(),
            HallHireTotals = monthlyTrends.Select(m => m.TotalHallHire).ToList(),

            // 🍩 Paid/Unpaid
            PaidCount = paidCount,
            UnpaidCount = unpaidCount,

            // 🥧 Spending Distribution
            TotalLeadFarmersSpending = totalLeadFarmersSpending,
            TotalEASpending = totalEASpending,
            TotalHallHireSpending = totalHallHireSpending
        };

        return View(model);
    }
}
