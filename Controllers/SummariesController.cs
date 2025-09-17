using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reports.Data;
using Reports.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Reports.Controllers
{
    public class SummariesController : Controller
    {
        private readonly ReportsDbContext _context;
        private readonly ILogger<SummariesController> _logger;

        public SummariesController(ReportsDbContext context, ILogger<SummariesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Summaries
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var reportsDbContext = _context.Summaries
                .Include(s => s.County)
                .Include(s => s.Venue)
                .Include(s => s.Voucher);
            return View(await reportsDbContext.ToListAsync());
        }

        // GET: Summaries/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var summary = await _context.Summaries
                .Include(s => s.County)
                .Include(s => s.Venue)
                .Include(s => s.Voucher)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (summary == null) return NotFound();

            return View(summary);
        }


        ////// GET: Summaries/Create
        ////public IActionResult Create()
        ////{
        ////    ViewData["PaymentVoucherId"] = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName");
        ////    ViewData["VenueId"] = new SelectList(_context.Venues, "Id", "Name");
        ////    ViewData["CountyId"] = new SelectList(_context.Counties, "Id", "CountyName");

        ////    return View();
        ////}

        ////[HttpPost]
        ////[ValidateAntiForgeryToken]
        ////public async Task<IActionResult> Create(Summary summary)
        ////{
        ////    if (ModelState.IsValid)
        ////    {
        ////        try
        ////        {
        ////            summary.Date = DateTime.Now;

        ////            // calculations
        ////            summary.HallHireTotal = (summary.HallHireNo * summary.EachHallAmount)
        ////                                    + (summary.HallHireNo * Convert.ToDecimal(summary.MpesaChargesPerHall));
        ////            summary.LeadFarmersTotal = (summary.LeadFarmersNo * summary.EachLeadFarmersAmount)
        ////                                       + (summary.LeadFarmersNo * Convert.ToDecimal(summary.MpesaChargesPerLeadFarmers));
        ////            summary.EATotal = (summary.EANo * summary.EachEAAmount)
        ////                              + (summary.EANo * Convert.ToDecimal(summary.MpesaChargesPerEA));
        ////            summary.SubTotalperVenue = summary.HallHireTotal + summary.LeadFarmersTotal + summary.EATotal;
        ////            summary.TotalSum = summary.SubTotalperVenue;

        ////            _context.Add(summary);
        ////            await _context.SaveChangesAsync();

        ////            TempData["SuccessMessage"] = "Summary created successfully!";
        ////            return RedirectToAction(nameof(Index));
        ////        }
        ////        catch (Exception ex)
        ////        {
        ////            TempData["ErrorMessage"] = "Error while creating summary: " + ex.Message;
        ////        }
        ////    }
        ////    else
        ////    {
        ////        TempData["ErrorMessage"] = "Validation failed. Please correct the errors and try again.";
        ////    }

        ////    // 👇 IMPORTANT: repopulate dropdowns if validation fails
        ////    ViewData["PaymentVoucherId"] = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName", summary.PaymentVoucherId);
        ////    ViewData["VenueId"] = new SelectList(_context.Venues, "Id", "Name", summary.VenueId);
        ////    ViewData["CountyId"] = new SelectList(_context.Counties, "Id", "CountyName", summary.CountyId);

        ////    TempData["ErrorMessage"] = "Something went wrong. Please check your inputs.";
        ////    return View(summary);
        ////}




        // GET: Summaries/Create
        [Authorize]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        // POST: Summaries/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Summary summary)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    summary.Date = DateTime.Now;

                    // calculations
                    summary.HallHireTotal = (summary.HallHireNo * summary.EachHallAmount)
                                            + (summary.HallHireNo * Convert.ToDecimal(summary.MpesaChargesPerHall));
                    summary.LeadFarmersTotal = (summary.LeadFarmersNo * summary.EachLeadFarmersAmount)
                                               + (summary.LeadFarmersNo * Convert.ToDecimal(summary.MpesaChargesPerLeadFarmers));
                    summary.EATotal = (summary.EANo * summary.EachEAAmount)
                                      + (summary.EANo * Convert.ToDecimal(summary.MpesaChargesPerEA));
                    summary.SubTotalperVenue = summary.HallHireTotal + summary.LeadFarmersTotal + summary.EATotal;
                    summary.TotalSum = summary.SubTotalperVenue;

                    _context.Add(summary);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Summary created successfully!";
                    return RedirectToAction(nameof(Index));
                }

                LogModelErrors();
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while creating Summary");
                ModelState.AddModelError("", "Database error: " + dbEx.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while creating Summary");
                ModelState.AddModelError("", "Unexpected error: " + ex.GetBaseException().Message);
            }

            PopulateDropdowns(summary);
            return View(summary);
        }

        // GET: Summaries/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var summary = await _context.Summaries.FindAsync(id);
            if (summary == null) return NotFound();

            PopulateDropdowns(summary);
            return View(summary);
        }

        // POST: Summaries/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Summary summary)
        {
            if (id != summary.Id) return NotFound();

            try
            {
                if (ModelState.IsValid)
                {
                    // recalc totals
                    summary.HallHireTotal = (summary.HallHireNo * summary.EachHallAmount)
                                            + (summary.HallHireNo * Convert.ToDecimal(summary.MpesaChargesPerHall));
                    summary.LeadFarmersTotal = (summary.LeadFarmersNo * summary.EachLeadFarmersAmount)
                                               + (summary.LeadFarmersNo * Convert.ToDecimal(summary.MpesaChargesPerLeadFarmers));
                    summary.EATotal = (summary.EANo * summary.EachEAAmount)
                                      + (summary.EANo * Convert.ToDecimal(summary.MpesaChargesPerEA));
                    summary.SubTotalperVenue = summary.HallHireTotal + summary.LeadFarmersTotal + summary.EATotal;
                    summary.TotalSum = summary.SubTotalperVenue;

                    _context.Update(summary);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }

                LogModelErrors();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!SummaryExists(summary.Id)) return NotFound();
                _logger.LogError(ex, "Concurrency error updating Summary id {Id}", id);
                ModelState.AddModelError("", "Concurrency error: " + ex.GetBaseException().Message);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while updating Summary id {Id}", id);
                ModelState.AddModelError("", "Database error: " + dbEx.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating Summary id {Id}", id);
                ModelState.AddModelError("", "Unexpected error: " + ex.GetBaseException().Message);
            }

            PopulateDropdowns(summary);
            return View(summary);
        }

        // GET: Summaries/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var summary = await _context.Summaries
                .Include(s => s.County)
                .Include(s => s.Venue)
                .Include(s => s.Voucher)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (summary == null) return NotFound();

            return View(summary);
        }

        // POST: Summaries/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var summary = await _context.Summaries.FindAsync(id);
            if (summary != null)
            {
                _context.Summaries.Remove(summary);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> VoucherList()
        {
            var vouchers = await _context.Summaries
                .Include(s => s.Voucher)
                .GroupBy(s => s.Voucher.VoucheName)
                .Select(g => new
                {
                    VoucheName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            return View(vouchers);
        }

        [Authorize]
        public async Task<IActionResult> GenerateSummaryPdf(string voucherName)
        {
            var summaries = await _context.Summaries
                .Include(s => s.Venue)
                .Include(s => s.County)
                .Include(s => s.Voucher)
                .Where(s => s.Voucher.VoucheName == voucherName)
                .ToListAsync();

            if (!summaries.Any())
            {
                return NotFound("No summaries found for this voucher.");
            }

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                // ✅ Use Times New Roman font, size 10
                var font = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                document.SetFont(font).SetFontSize(10);

                var doc = new Document(pdf, PageSize.A4);
                //document.SetFont(times);

                // 🔹 Default font size = 10
                document.SetFontSize(10);

                // --- Title ---
                var title = new Paragraph("SUMMARIES")
                    .SetFont(font)
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetUnderline(0.5f, -1);

                decimal grandTotal = 0;

                // ===== DATE ROW =====
                Table dateTable = new Table(new float[] { 1, 3, 3 })
                    .UseAllAvailableWidth()
                    .SetFontSize(10)
                    .SetFontSize(10);

                dateTable.AddCell(new Cell().Add(new Paragraph("DATE").SetFontSize(10)));
                dateTable.AddCell(new Cell(1, 2).Add(new Paragraph($"{summaries.First().Date:dd/MM/yyyy}").SetFontSize(10)));
                document.Add(dateTable);

                foreach (var summary in summaries)
                {                    

                    // ===== VENUE HEADER =====
                    Table venueTable = new Table(new float[] { 1, 3, 3 })
                        .UseAllAvailableWidth()
                        .SetFontSize(10);

                    venueTable.AddCell(new Cell().Add(new Paragraph("VENUE").SetFontSize(10)));
                    venueTable.AddCell(new Cell(1, 2).Add(new Paragraph(summary.Venue?.Name ?? "").SetFontSize(10)));
                    document.Add(venueTable);

                    // ===== MAIN TABLE =====
                    Table table = new Table(new float[] { 3, 1, 1, 2, 2 })
                        .UseAllAvailableWidth()
                        .SetFontSize(10);

                    // Hall Hire
                    table.AddCell(new Paragraph("HALL HIRE").SetFontSize(10));
                    table.AddCell(new Paragraph(summary.HallHireNo.ToString()).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.EachHallAmount.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.HallHireTotal.ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("PAID").SetFontSize(10));

                    table.AddCell(new Paragraph("MPESA CHARGES").SetFontSize(10));
                    table.AddCell(new Paragraph("1").SetFontSize(10)); // Always 1 for hall hire
                    table.AddCell(new Paragraph(summary.MpesaChargesPerHall.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph((summary.HallHireNo * summary.MpesaChargesPerHall).ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("PAID").SetFontSize(10));

                    // Lead Farmers
                    table.AddCell(new Paragraph("LEAD FARMERS").SetFontSize(10));
                    table.AddCell(new Paragraph(summary.LeadFarmersNo.ToString()).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.EachLeadFarmersAmount.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph((summary.LeadFarmersNo * summary.EachLeadFarmersAmount).ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("").SetFontSize(10));

                    table.AddCell(new Paragraph("MPESA CHARGES").SetFontSize(10));
                    table.AddCell(new Paragraph(summary.LeadFarmersNo.ToString()).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.MpesaChargesPerLeadFarmers.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph((summary.LeadFarmersNo * summary.MpesaChargesPerLeadFarmers).ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("").SetFontSize(10));

                    // EA
                    table.AddCell(new Paragraph("E.A").SetFontSize(10));
                    table.AddCell(new Paragraph(summary.EANo.ToString()).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.EachEAAmount.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph((summary.EANo * summary.EachEAAmount).ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("").SetFontSize(10));

                    table.AddCell(new Paragraph("MPESA CHARGES").SetFontSize(10));
                    table.AddCell(new Paragraph(summary.EANo.ToString()).SetFontSize(10));
                    table.AddCell(new Paragraph(summary.MpesaChargesPerEA.ToString("N0")).SetFontSize(10));
                    table.AddCell(new Paragraph((summary.EANo * summary.MpesaChargesPerEA).ToString("N2")).SetFontSize(10));
                    table.AddCell(new Paragraph("").SetFontSize(10));

                    // Subtotal row
                    table.AddCell(new Cell(1, 4).Add(new Paragraph("sub total").SetFontSize(10)));
                    table.AddCell(new Paragraph(summary.SubTotalperVenue.ToString("N2")).SetFontSize(10));

                    document.Add(table);

                    grandTotal += summary.SubTotalperVenue;
                }

                // ===== GRAND TOTAL =====
                Table sumTable = new Table(new float[] { 1, 3, 3 })
                    .UseAllAvailableWidth()
                    .SetFontSize(10);

                sumTable.AddCell(new Cell().Add(new Paragraph("SUM").SetFontSize(10)));
                sumTable.AddCell(new Cell(1, 2).Add(new Paragraph(grandTotal.ToString("N2")).SetFontSize(10)));
                document.Add(sumTable);

                document.Close();

                return File(ms.ToArray(), "application/pdf", $"{voucherName}_Summary.pdf");
            }
        }


        ////[Authorize]
        ////public async Task<IActionResult> GenerateSummaryPdf(string voucherName)
        ////{
        ////    var summaries = await _context.Summaries
        ////        .Include(s => s.Venue)
        ////        .Include(s => s.County)
        ////        .Include(s => s.Voucher)
        ////        .Where(s => s.Voucher.VoucheName == voucherName)
        ////        .ToListAsync();

        ////    if (!summaries.Any())
        ////    {
        ////        return NotFound("No summaries found for this voucher.");
        ////    }

        ////    using (var ms = new MemoryStream())
        ////    {
        ////        var writer = new PdfWriter(ms);
        ////        var pdf = new PdfDocument(writer);
        ////        var document = new Document(pdf);

        ////        decimal grandTotal = 0;

        ////        // ===== DATE ROW =====
        ////        Table dateTable = new Table(new float[] { 1, 3, 3 })
        ////            .UseAllAvailableWidth();

        ////        dateTable.AddCell(new Cell().Add(new Paragraph("DATE")));
        ////        dateTable.AddCell(new Cell(1, 2).Add(new Paragraph($"{summaries.First().Date:dd/MM/yyyy}")));
        ////        document.Add(dateTable);

        ////        foreach (var summary in summaries)
        ////        {
        ////            // ===== VENUE HEADER =====
        ////            Table venueTable = new Table(new float[] { 1, 3, 3 })
        ////                .UseAllAvailableWidth();
        ////            venueTable.AddCell(new Cell().Add(new Paragraph("VENUE")));
        ////            venueTable.AddCell(new Cell(1, 2).Add(new Paragraph(summary.Venue?.Name ?? "")));
        ////            document.Add(venueTable);

        ////            // ===== MAIN TABLE =====
        ////            Table table = new Table(new float[] { 3, 1, 1, 2, 2 })
        ////                .UseAllAvailableWidth();

        ////            // Hall Hire
        ////            table.AddCell("HALL HIRE");
        ////            table.AddCell(summary.HallHireNo.ToString());
        ////            table.AddCell(summary.EachHallAmount.ToString("N0"));
        ////            table.AddCell(summary.HallHireTotal.ToString("N2"));
        ////            table.AddCell("PAID");

        ////            table.AddCell("MPESA CHARGES");
        ////            table.AddCell("1"); // Always 1 for hall hire
        ////            table.AddCell(summary.MpesaChargesPerHall.ToString("N0"));
        ////            table.AddCell((summary.HallHireNo * summary.MpesaChargesPerHall).ToString("N2"));
        ////            table.AddCell("PAID");

        ////            // Lead Farmers
        ////            table.AddCell("LEAD FARMERS");
        ////            table.AddCell(summary.LeadFarmersNo.ToString());
        ////            table.AddCell(summary.EachLeadFarmersAmount.ToString("N0"));
        ////            table.AddCell((summary.LeadFarmersNo * summary.EachLeadFarmersAmount).ToString("N2"));
        ////            table.AddCell("");

        ////            table.AddCell("MPESA CHARGES");
        ////            table.AddCell(summary.LeadFarmersNo.ToString());
        ////            table.AddCell(summary.MpesaChargesPerLeadFarmers.ToString("N0"));
        ////            table.AddCell((summary.LeadFarmersNo * summary.MpesaChargesPerLeadFarmers).ToString("N2"));
        ////            table.AddCell("");

        ////            // EA
        ////            table.AddCell("E.A");
        ////            table.AddCell(summary.EANo.ToString());
        ////            table.AddCell(summary.EachEAAmount.ToString("N0"));
        ////            table.AddCell((summary.EANo * summary.EachEAAmount).ToString("N2"));
        ////            table.AddCell("");

        ////            table.AddCell("MPESA CHARGES");
        ////            table.AddCell(summary.EANo.ToString());
        ////            table.AddCell(summary.MpesaChargesPerEA.ToString("N0"));
        ////            table.AddCell((summary.EANo * summary.MpesaChargesPerEA).ToString("N2"));
        ////            table.AddCell("");

        ////            // Subtotal row (merged cells)
        ////            table.AddCell(new Cell(1, 4).Add(new Paragraph("sub total")));
        ////            table.AddCell(summary.SubTotalperVenue.ToString("N2"));

        ////            document.Add(table);

        ////            grandTotal += summary.SubTotalperVenue;
        ////        }

        ////        // ===== GRAND TOTAL =====
        ////        Table sumTable = new Table(new float[] { 1, 3, 3 })
        ////            .UseAllAvailableWidth();

        ////        sumTable.AddCell(new Cell().Add(new Paragraph("SUM")));
        ////        sumTable.AddCell(new Cell(1, 2).Add(new Paragraph(grandTotal.ToString("N2"))));
        ////        document.Add(sumTable);

        ////        document.Close();

        ////        return File(ms.ToArray(), "application/pdf", $"{voucherName}_Summary.pdf");
        ////    }
        ////}

        private bool SummaryExists(int id)
        {
            return _context.Summaries.Any(e => e.Id == id);
        }

        private void PopulateDropdowns(Summary? summary = null)
        {
            ViewData["CountyId"] = new SelectList(_context.Counties, "Id", "Name", summary?.CountyId);
            ViewData["VenueId"] = new SelectList(_context.Venues, "Id", "Name", summary?.VenueId);
            ViewData["PaymentVoucherId"] = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName", summary?.PaymentVoucherId);
        }

        private void LogModelErrors()
        {
            var modelErrors = ModelState
                .Where(kvp => kvp.Value.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value.Errors.Select(err => new { kvp.Key, Err = err }))
                .Select(x => $"{x.Key}: {(string.IsNullOrEmpty(x.Err.ErrorMessage) ? x.Err.Exception?.Message : x.Err.ErrorMessage)}")
                .ToArray();

            var combined = modelErrors.Length > 0 ? string.Join(" | ", modelErrors) : "ModelState invalid.";
            ModelState.AddModelError("", "Validation failed: " + combined);
            _logger.LogWarning("Validation errors: {Errors}", combined);
        }
    }
}
