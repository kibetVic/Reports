using DocumentFormat.OpenXml.Office2019.Drawing.Animation.Model3D;
using DocumentFormat.OpenXml.Packaging;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf.IO;
using Reports.Data;
using Reports.Migrations;
using Reports.Models;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Path = System.IO.Path;
using PdfReader = iText.Kernel.Pdf.PdfReader;
using Receipt = Reports.Models.Receipt;

namespace Reports.Controllers
{
    public class ReceiptsController : Controller
    {
        private readonly ReportsDbContext _context;

        public ReceiptsController(ReportsDbContext context)
        {
            _context = context;
        }

        // GET: Receipts
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var Receipts = await _context.Receipts
                .Include(q => q.Items)
                .OrderByDescending(q => q.DateCreated)
                .ToListAsync();

            return View(Receipts);
        }

        // GET: Receipts/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var Receipt = await _context.Receipts
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (Receipt == null) return NotFound();

            return View(Receipt);
        }

        // GET: Receipt/Create
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var model = new Receipt
            {
                ReceiptNumber = await GenerateReceiptNumber(),
                DateCreated = DateTime.Now
            };
            ViewData["StatusList"] = new SelectList(Enum.GetValues(typeof(ReceiptStatus)));
            return View(model);
        }

        // POST: Receipts/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Receipt Receipts, bool AddVAT = false)
        {
            try
            {
                // Filter invalid or blank rows
                Receipts.Items = Receipts.Items?
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description)
                                && i.Qty.HasValue && i.Qty.Value > 0
                                && i.PricePerQty.HasValue && i.PricePerQty.Value > 0)
                    .ToList() ?? new List<ReceiptItem>();

                if (string.IsNullOrWhiteSpace(Receipts.ClientName))
                    ModelState.AddModelError("ClientName", "Client name is required.");

                if (!Receipts.Items.Any())
                    ModelState.AddModelError("", "Please add at least one item before saving.");

                if (!ModelState.IsValid)
                {
                    var allErrors = ModelState.Values.SelectMany(v => v.Errors)
                                                     .Select(e => e.ErrorMessage);
                    ViewBag.ErrorMessage = "Some fields are missing or invalid: " + string.Join(" | ", allErrors);
                    return View(Receipts);
                }

                // ✅ Compute totals
                Receipts.SubTotal = Receipts.Items.Sum(i => i.Qty.Value * i.PricePerQty.Value);
                Receipts.TotalItems = Receipts.Items.Sum(i => i.Qty ?? 0);
                Receipts.VAT = AddVAT ? Receipts.SubTotal * 0.16m : 0m; // Only add VAT if checked
                Receipts.TotalAmount = Receipts.SubTotal + Receipts.VAT;

                // ✅ Save Receipts
                _context.Receipts.Add(Receipts);
                await _context.SaveChangesAsync();

                // ✅ Link items
                foreach (var item in Receipts.Items)
                {
                    item.Amount = item.Qty.Value * item.PricePerQty.Value;
                    item.ReceiptId = Receipts.Id;
                    item.Receipt = null;
                    _context.ReceiptItems.Add(item);
                }

                await _context.SaveChangesAsync();

                ViewData["StatusList"] = new SelectList(Enum.GetValues(typeof(ReceiptStatus)), Receipts.Status);
                TempData["SuccessMessage"] = "Receipts saved successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                return View(Receipts);
            }
        }


        // GET: Receipts/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var Receipts = await _context.Receipts
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (Receipts == null) return NotFound();

            // ✅ Pre-check the VAT checkbox if Receipts has VAT already
            Receipts.AddVAT = Receipts.VAT > 0;

            // ✅ Populate dropdown for ReceiptStatus
            ViewData["StatusList"] = new SelectList(Enum.GetValues(typeof(ReceiptStatus)), Receipts.Status);
            return View(Receipts);
        }

        // POST: Receipts/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Receipt Receipts)
        {
            if (id != Receipts.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // ✅ Load existing Receipts with items
                    var existingReceipts = await _context.Receipts
                        .Include(q => q.Items)
                        .FirstOrDefaultAsync(q => q.Id == id);

                    if (existingReceipts == null)
                        return NotFound();

                    // ✅ Update Receipt fields
                    existingReceipts.ClientName = Receipts.ClientName;
                    existingReceipts.DateCreated = Receipts.DateCreated;
                    existingReceipts.RefferenceNo = Receipts.RefferenceNo;

                    // ✅ Update Receipt Status
                    existingReceipts.Status = Receipts.Status;

                    // ✅ Recalculate totals
                    existingReceipts.SubTotal = Receipts.Items.Sum(i => i.Amount);
                    existingReceipts.TotalItems = Receipts.Items.Sum(i => i.Qty ?? 0);

                    // ✅ Apply VAT if checked
                    existingReceipts.VAT = Receipts.AddVAT ? existingReceipts.SubTotal * 0.16m : 0m;
                    existingReceipts.TotalAmount = existingReceipts.SubTotal + existingReceipts.VAT;

                    // ✅ Clear old items
                    _context.ReceiptItems.RemoveRange(existingReceipts.Items);

                    // ✅ Add updated items
                    existingReceipts.Items = Receipts.Items.Select(i => new ReceiptItem
                    {
                        Description = i.Description,
                        Qty = i.Qty,
                        PricePerQty = i.PricePerQty,
                        Amount = i.Amount
                    }).ToList();

                    // ✅ Save changes
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceiptsExists(Receipts.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            ViewData["StatusList"] = new SelectList(Enum.GetValues(typeof(ReceiptStatus)), Receipts.Status);
            return View(Receipts);
        }

        // GET: Receipts/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var Receipts = await _context.Receipts
                .Include(q => q.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (Receipts == null) return NotFound();

            return View(Receipts);
        }

        // POST: Receipts/DeleteConfirmed/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var Receipts = await _context.Receipts
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (Receipts != null)
            {
                _context.ReceiptItems.RemoveRange(Receipts.Items);
                _context.Receipts.Remove(Receipts);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ReceiptsExists(int id)
        {
            return _context.Receipts.Any(e => e.Id == id);
        }

        [Authorize]
        private async Task<string> GenerateReceiptNumber()
        {
            string prefix = "AMT";
            string currentYear = DateTime.Now.Year.ToString();

            // Find the last Receipts for the current year
            var lastReceipts = await _context.Receipts
                .Where(q => q.ReceiptNumber.StartsWith($"{prefix}{currentYear}"))
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 123; // Start from 123 if none exists

            if (lastReceipts != null && !string.IsNullOrEmpty(lastReceipts.ReceiptNumber))
            {
                // Extract numeric part after the year (e.g., AMT2025123 → 123)
                string numericPart = lastReceipts.ReceiptNumber.Substring(prefix.Length + currentYear.Length);
                if (int.TryParse(numericPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            // Result: AMT2025123, AMT2025124, etc.
            return $"{prefix}{currentYear}{nextNumber}";
        }


        [Authorize]
        public async Task<IActionResult> GenerateReceiptPdf(int id)
        {
            var Receipts = await _context.Receipts
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (Receipts == null)
                return NotFound();

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, new PageSize(250, 800)); // Slim receipt size
                document.SetMargins(20, 20, 20, 20);

                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.COURIER);
                document.SetFont(font);
                document.SetFontSize(9);

                // --- HEADER ---
                document.Add(new Paragraph("AMTECH TECHNOLOGIES LTD")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBold()
                    .SetFontSize(10));
                document.Add(new Paragraph("P.O BOX 79701 - 00200, NAIROBI")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("TEL: 0792 716 541/0734 871 556")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("Email: info@amtechafrica.com")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("--------------------------------------"));

                // --- RECEIPT INFO ---
                document.Add(new Paragraph($"Receipt No: {Receipts.ReceiptNumber ?? "-"}"));
                document.Add(new Paragraph($"Date: {Receipts.DateCreated:dd/MM/yyyy}"));
                document.Add(new Paragraph($"Customer: {Receipts.ClientName ?? "Walk-in"}"));
                document.Add(new Paragraph($"Phone NO: {Receipts.PhoneNomber ?? "-"}"));
                document.Add(new Paragraph($"ReffenceNo / ChequeNo: {Receipts.RefferenceNo ?? "-"}"));
                document.Add(new Paragraph("--------------------------------------"));

                // --- ITEMS TABLE ---
                Table itemTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 })).UseAllAvailableWidth();
                itemTable.AddCell(new Cell().Add(new Paragraph("Item").SetBold()).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell().Add(new Paragraph("Qty").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell().Add(new Paragraph("Amt").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell(1, 3).Add(new Paragraph("----------------------------------")).SetBorder(Border.NO_BORDER));

                foreach (var item in Receipts.Items)
                {
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Description ?? "-")).SetBorder(Border.NO_BORDER));
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{item.Qty ?? 0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{item.Amount:N0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                }

                document.Add(itemTable);
                document.Add(new Paragraph("--------------------------------------"));

                // --- TOTALS ---
                document.Add(new Paragraph($"Sub Total:      {Receipts.SubTotal:N0}")
                    .SetTextAlignment(TextAlignment.RIGHT));

                if (Receipts.AddVAT)
                {
                    document.Add(new Paragraph($"VAT (16%):      {Receipts.VAT:N0}")
                        .SetTextAlignment(TextAlignment.RIGHT));
                }

                document.Add(new Paragraph($"TOTAL:          {Receipts.TotalAmount:N0}")
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetBold());

                document.Add(new Paragraph("--------------------------------------"));

                // --- FOOTER ---
                document.Add(new Paragraph("UNDERSTANDING YOUR BUSINESS BETTER!")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(9)
                    .SetBold());
                document.Add(new Paragraph("Visit Again")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("--------------------------------------"));
                document.Add(new Paragraph("Powered by Amtech Africa")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(8)
                    .SetFontColor(ColorConstants.GRAY));

                // ✅ --- DYNAMIC STAMP (PAID / RECEIVED) ---
                string stampTextValue = Receipts.Status switch
                {
                    ReceiptStatus.Paid => "PAID",
                    ReceiptStatus.Received => "RECEIVED",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(stampTextValue))
                {
                    // Create styled stamp paragraph
                    var stampText = new Paragraph()
                        .Add(new Text("AMTECH TECHNOLOGIES LTD\n")
                            .SetFontSize(9)
                            .SetFontColor(new DeviceRgb(0, 102, 204)))
                        .Add(new Text("P.O BOX 79701 - 00200, NAIROBI\n")
                            .SetFontSize(9)
                            .SetFontColor(new DeviceRgb(0, 102, 204)))
                        .Add(new Text($"DATE: {Receipts.DateCreated:dd/MM/yyyy}\n")
                            .SetFontSize(9)
                            .SetFontColor(new DeviceRgb(0, 102, 204)))
                        .Add(new Text($"{stampTextValue}\n") // ← Dynamic value here
                            .SetFontSize(18)
                            .SetBold()
                            .SetFontColor(new DeviceRgb(0, 102, 204)));

                    // --- Centered & angled stamp ---
                    var page = pdf.GetPage(pdf.GetNumberOfPages());
                    var pageSize = page.GetPageSize();
                    float centerX = pageSize.GetWidth() / 2;
                    float centerY = pageSize.GetHeight() / 2;

                    document.ShowTextAligned(
                        stampText,
                        centerX,
                        centerY,
                        pdf.GetNumberOfPages(),
                        TextAlignment.CENTER,
                        VerticalAlignment.MIDDLE,
                        (float)(Math.PI / 6) // angled like real stamp
                    );
                }

                document.Close();
                return File(ms.ToArray(), "application/pdf", $"Receipt_{Receipts.ReceiptNumber}.pdf");
            }
        }
    }
}
