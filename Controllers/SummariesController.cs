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
                //.Include(s => s.status)
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
                //.Include(s => s.Status)
                .Include(s => s.Voucher)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (summary == null) return NotFound();

            return View(summary);
        }


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

                    // Hall Hire
                    summary.HallHireTotal = summary.HallHireNo * summary.EachHallAmount;
                    summary.TotalMpesaChargesPerHall = summary.HallHireNo * Convert.ToDecimal(summary.MpesaChargesPerHall);

                    // Lead Farmers
                    summary.LeadFarmersTotal = summary.LeadFarmersNo * summary.EachLeadFarmersAmount;
                    summary.TotalMpesaChargesPerLeadFarmers = summary.LeadFarmersNo * Convert.ToDecimal(summary.MpesaChargesPerLeadFarmers);

                    // EA
                    summary.EATotal = summary.EANo * summary.EachEAAmount;
                    summary.TotalMpesaChargesPerEA = summary.EANo * Convert.ToDecimal(summary.MpesaChargesPerEA);

                    // Venue subtotal (all items + all mpesa charges)
                    summary.SubTotalperVenue =
                        summary.HallHireTotal +
                        summary.TotalMpesaChargesPerHall +
                        summary.LeadFarmersTotal +
                        summary.TotalMpesaChargesPerLeadFarmers +
                        summary.EATotal +
                        summary.TotalMpesaChargesPerEA;

                    // If you still need TotalSum separately, keep it; otherwise remove
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
                    // Hall Hire
                    summary.HallHireTotal = summary.HallHireNo * summary.EachHallAmount;
                    summary.TotalMpesaChargesPerHall = summary.HallHireNo * Convert.ToDecimal(summary.MpesaChargesPerHall);

                    // Lead Farmers
                    summary.LeadFarmersTotal = summary.LeadFarmersNo * summary.EachLeadFarmersAmount;
                    summary.TotalMpesaChargesPerLeadFarmers = summary.LeadFarmersNo * Convert.ToDecimal(summary.MpesaChargesPerLeadFarmers);

                    // EA
                    summary.EATotal = summary.EANo * summary.EachEAAmount;
                    summary.TotalMpesaChargesPerEA = summary.EANo * Convert.ToDecimal(summary.MpesaChargesPerEA);

                    // Venue subtotal (all items + all mpesa charges)
                    summary.SubTotalperVenue =
                        summary.HallHireTotal +
                        summary.TotalMpesaChargesPerHall +
                        summary.LeadFarmersTotal +
                        summary.TotalMpesaChargesPerLeadFarmers +
                        summary.EATotal +
                        summary.TotalMpesaChargesPerEA;

                    // If you still need TotalSum separately, keep it; otherwise remove
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
                //.Include(s => s.Venue)
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


        private bool SummaryExists(int id)
        {
            return _context.Summaries.Any(e => e.Id == id);
        }

        private void PopulateDropdowns(Summary? summary = null)
        {
            ViewData["CountyId"] = new SelectList(_context.Counties, "Id", "Name", summary?.CountyId);
            ViewData["PaymentVoucherId"] = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName", summary?.PaymentVoucherId);
        }

        [Authorize]
        public async Task<IActionResult> GenerateSummaryPdf(string voucherName)
        {
            var summaries = await _context.Summaries
                .Include(s => s.County)
                .Include(s => s.Voucher)
                .Where(s => s.Voucher.VoucheName == voucherName)
                .ToListAsync();

            if (!summaries.Any())
                return NotFound("No summaries found for this voucher.");

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf);

                var font = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                document.SetFont(font).SetFontSize(10);

                // Title
                var title = new Paragraph("SUMMARIES")
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetUnderline(0.5f, -1);
                document.Add(title);

                decimal grandTotal = 0;

                // Date Table
                Table dateTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3 }))
                    .UseAllAvailableWidth();

                dateTable.AddCell(new Cell().Add(new Paragraph("DATE")));
                dateTable.AddCell(new Cell().Add(new Paragraph($"{summaries.First().Date:dd/MM/yyyy}")));
                document.Add(dateTable);

                foreach (var summary in summaries)
                {
                    // Venue Name Row
                    Table venueTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3 }))
                        .UseAllAvailableWidth();
                    venueTable.AddCell(new Cell().Add(new Paragraph("VENUE")));
                    venueTable.AddCell(new Cell().Add(new Paragraph(summary.Venue?.ToString())));
                    document.Add(venueTable);

                    // Itemized Table: Label | Qty | Unit Price | Total | Paid (optional)
                    Table itemTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1, 1, 2, 1 }))
                        .UseAllAvailableWidth();

                    // Helper function to add a row
                    void AddRow(string label, int qty, decimal unitPrice, decimal total, bool showPaid)
                    {
                        itemTable.AddCell(new Paragraph(label));
                        itemTable.AddCell(new Paragraph(qty.ToString()));
                        itemTable.AddCell(new Paragraph(unitPrice.ToString("N0")));
                        itemTable.AddCell(new Paragraph(total.ToString("N2")));
                        itemTable.AddCell(new Paragraph(showPaid ? "PAID" : ""));
                    }

                    // Add rows in same order as image
                    AddRow("HALL HIRE", summary.HallHireNo, summary.EachHallAmount, summary.HallHireTotal, summary.status == PaymentStatus.Paid);
                    AddRow("MPESA CHARGES", summary.HallHireNo, summary.MpesaChargesPerHall, summary.TotalMpesaChargesPerHall, summary.status == PaymentStatus.Paid);

                    AddRow("LEAD FARMERS", summary.LeadFarmersNo, summary.EachLeadFarmersAmount, summary.LeadFarmersTotal, false);
                    AddRow("MPESA CHARGES", summary.LeadFarmersNo, summary.MpesaChargesPerLeadFarmers, summary.TotalMpesaChargesPerLeadFarmers, false);

                    AddRow("E.A", summary.EANo, summary.EachEAAmount, summary.EATotal, false);
                    AddRow("MPESA CHARGES", summary.EANo, summary.MpesaChargesPerEA, summary.TotalMpesaChargesPerEA, false);

                    // Sub Total Row
                    itemTable.AddCell(new Cell(1, 3).Add(new Paragraph("sub total")));
                    itemTable.AddCell(new Paragraph(summary.SubTotalperVenue.ToString("N2")));
                    itemTable.AddCell(new Paragraph(""));

                    document.Add(itemTable);
                    grandTotal += summary.SubTotalperVenue;
                }

                // Final SUM row
                Table sumTable = new Table(UnitValue.CreatePercentArray(new float[] {1, 3, 3 }))
                    .UseAllAvailableWidth();
                sumTable.AddCell(new Cell(5,2).Add(new Paragraph("SUM")));
                sumTable.AddCell(new Cell().Add(new Paragraph(grandTotal.ToString("N2"))));
                document.Add(sumTable);

                document.Close();
                return File(ms.ToArray(), "application/pdf", $"{voucherName}_Summary.pdf");
            }
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
