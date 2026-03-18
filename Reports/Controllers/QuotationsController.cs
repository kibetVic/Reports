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
using Reports.Models;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Path = System.IO.Path;
using PdfReader = iText.Kernel.Pdf.PdfReader;

namespace Reports.Controllers
{
    public class QuotationController : Controller
    {
        private readonly ReportsDbContext _context;

        public QuotationController(ReportsDbContext context)
        {
            _context = context;
        }

        // GET: Quotation
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var quotations = await _context.Quotations
                .Include(q => q.Items)
                .OrderByDescending(q => q.DateCreated)
                .ToListAsync();

            return View(quotations);
        }

        // GET: Quotation/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null) return NotFound();

            return View(quotation);
        }

        // GET: Quotation/Create
        public async Task<IActionResult> Create()
        {
            var model = new Quotation
            {
                QuotationNumber = await GenerateQuotationNumber(),
                DateCreated = DateTime.Now
            };
            return View(model);
        }

        // POST: Quotation/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Quotation quotation, bool AddVAT = false)
        {
            try
            {
                // Filter invalid or blank rows
                quotation.Items = quotation.Items?
                    .Where(i => !string.IsNullOrWhiteSpace(i.Description)
                                && i.Qty.HasValue && i.Qty.Value > 0
                                && i.PricePerQty.HasValue && i.PricePerQty.Value > 0)
                    .ToList() ?? new List<QuotationItem>();

                if (string.IsNullOrWhiteSpace(quotation.ClientName))
                    ModelState.AddModelError("ClientName", "Client name is required.");

                if (!quotation.Items.Any())
                    ModelState.AddModelError("", "Please add at least one item before saving.");

                if (!ModelState.IsValid)
                {
                    var allErrors = ModelState.Values.SelectMany(v => v.Errors)
                                                     .Select(e => e.ErrorMessage);
                    ViewBag.ErrorMessage = "Some fields are missing or invalid: " + string.Join(" | ", allErrors);
                    return View(quotation);
                }

                // ✅ Compute totals
                quotation.SubTotal = quotation.Items.Sum(i => i.Qty.Value * i.PricePerQty.Value);
                quotation.TotalItems = quotation.Items.Sum(i => i.Qty ?? 0);
                quotation.VAT = AddVAT ? quotation.SubTotal * 0.16m : 0m; // Only add VAT if checked
                quotation.TotalAmount = quotation.SubTotal + quotation.VAT;

                // ✅ Save quotation
                _context.Quotations.Add(quotation);
                await _context.SaveChangesAsync();

                // ✅ Link items
                foreach (var item in quotation.Items)
                {
                    item.Amount = item.Qty.Value * item.PricePerQty.Value;
                    item.QuotationId = quotation.Id;
                    item.Quotation = null;
                    _context.QuotationItems.Add(item);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quotation saved successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"An unexpected error occurred: {ex.Message}";
                return View(quotation);
            }
        }


        // GET: Quotation/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null) return NotFound();

            // ✅ Pre-check the VAT checkbox if quotation has VAT already
            quotation.AddVAT = quotation.VAT > 0;

            return View(quotation);
        }

        // POST: Quotation/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Quotation quotation)
        {
            if (id != quotation.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // ✅ Load existing quotation with items
                    var existingQuotation = await _context.Quotations
                        .Include(q => q.Items)
                        .FirstOrDefaultAsync(q => q.Id == id);

                    if (existingQuotation == null)
                        return NotFound();

                    // ✅ Update quotation fields
                    existingQuotation.ClientName = quotation.ClientName;
                    existingQuotation.DateCreated = quotation.DateCreated;

                    // ✅ Recalculate totals
                    existingQuotation.SubTotal = quotation.Items.Sum(i => i.Amount);
                    existingQuotation.TotalItems = quotation.Items.Sum(i => i.Qty ?? 0);

                    // ✅ Apply VAT if checked
                    existingQuotation.VAT = quotation.AddVAT ? existingQuotation.SubTotal * 0.16m : 0m;
                    existingQuotation.TotalAmount = existingQuotation.SubTotal + existingQuotation.VAT;

                    // ✅ Clear old items
                    _context.QuotationItems.RemoveRange(existingQuotation.Items);

                    // ✅ Add updated items
                    existingQuotation.Items = quotation.Items.Select(i => new QuotationItem
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
                    if (!QuotationExists(quotation.Id))
                        return NotFound();
                    else
                        throw;
                }
            }

            return View(quotation);
        }





        // GET: Quotation/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (quotation == null) return NotFound();

            return View(quotation);
        }

        // POST: Quotation/DeleteConfirmed/5
        [HttpPost, ActionName("DeleteConfirmed")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation != null)
            {
                _context.QuotationItems.RemoveRange(quotation.Items);
                _context.Quotations.Remove(quotation);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool QuotationExists(int id)
        {
            return _context.Quotations.Any(e => e.Id == id);
        }

        [Authorize]

        private async Task<string> GenerateQuotationNumber()
        {
            string prefix = "AMT";
            string currentYear = DateTime.Now.Year.ToString();

            // Find the last quotation for the current year
            var lastQuotation = await _context.Quotations
                .Where(q => q.QuotationNumber.StartsWith($"{prefix}{currentYear}"))
                .OrderByDescending(q => q.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 123; // Start from 123 if none exists

            if (lastQuotation != null && !string.IsNullOrEmpty(lastQuotation.QuotationNumber))
            {
                // Extract numeric part after the year (e.g., AMT2025123 → 123)
                string numericPart = lastQuotation.QuotationNumber.Substring(prefix.Length + currentYear.Length);
                if (int.TryParse(numericPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            // Result: AMT2025123, AMT2025124, etc.
            return $"{prefix}{currentYear}{nextNumber}";
        }



        // 🔹 Generate PDF for selected quotation
        [Authorize]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
                return NotFound();

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4); // ✅ Portrait mode
                document.SetMargins(40, 40, 60, 40);

                // ✅ Set font to Times New Roman (12pt)
                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                document.SetFont(font);
                document.SetFontSize(12);

                // --- STAMP SECTION ---
                var stampFrame = new Div()
                    .SetBorder(new DashedBorder(new DeviceRgb(173, 216, 230), 0.5f))
                    .SetPadding(3)
                    .SetWidth(150)
                    .SetHeight(100)
                    .SetTextAlignment(TextAlignment.CENTER);

                // Build the text inside the stamp
                var stampText = new Paragraph()
                    .Add(new Text("AMTECH TECHNOLOGIES LIMITED\n")
                        .SetFontSize(8)
                        .SetBold()
                        .SetFontColor(new DeviceRgb(173, 216, 230)))
                    .Add(new Text("P.O. BOX 79701-00200, NAIROBI\n")
                        .SetFontSize(8)
                        .SetBold()
                        .SetFontColor(new DeviceRgb(173, 216, 230)))
                    .Add(new Text("DATE: ")
                        .SetFontSize(10)
                        .SetFontColor(new DeviceRgb(173, 216, 230)))
                    .Add(new Text($"{quotation.DateCreated:dd/MM/yyyy}\n")  // ✅ Correct variable name (lowercase q)
                        .SetFontSize(10)
                        .SetFontColor(new DeviceRgb(173, 216, 230)));

                // Add the stamp to the PDF — rotated 30 degrees like a real stamp
                document.ShowTextAligned(
                    stampText,
                    400, 400, // adjust X and Y position to match your layout
                    pdf.GetNumberOfPages(),
                    TextAlignment.CENTER,
                    VerticalAlignment.MIDDLE,
                    (float)(Math.PI / 6) // 30° rotation
                );


                // --- LOGO ---
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/amtech_logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = new iText.Layout.Element.Image(ImageDataFactory.Create(logoPath))
                        .ScaleToFit(220, 80)
                        .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                        .SetMarginBottom(5);
                    document.Add(logo);
                }

                // --- TITLE & QUOTATION NUMBER ---
                Table titleTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                    .UseAllAvailableWidth();

                // Left cell — title
                titleTable.AddCell(new Cell()
                    .Add(new Paragraph("QUOTATION")
                        .SetFontSize(16)
                        .SetBold()
                        .SetTextAlignment(TextAlignment.LEFT)
                        .SetMarginBottom(1))
                    .SetBorder(Border.NO_BORDER));

                // Right cell — quotation number
                titleTable.AddCell(new Cell()
                    .Add(new Paragraph($"Quotation No: {quotation.QuotationNumber}")
                        .SetFontSize(12)
                        .SetTextAlignment(TextAlignment.RIGHT)
                        .SetMarginBottom(1))
                    .SetBorder(Border.NO_BORDER));

                // Add the title table
                document.Add(titleTable);

                // Client info (no vertical spacing between lines)
                document.Add(new Paragraph($"Client Name: {quotation.ClientName ?? ""}")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginBottom(0)
                    .SetMultipliedLeading(1));

                document.Add(new Paragraph($"Phone Number: {quotation.PhoneNomber ?? ""}")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetMarginTop(0)
                    .SetMultipliedLeading(1));

                // --- ITEM TABLE ---
                Table itemTable = new Table(new float[] { 0.6f, 4.5f, 1f, 2f, 2f }).UseAllAvailableWidth();

                // --- HEADER ROW ---
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("#").SetBold().SetTextAlignment(TextAlignment.CENTER)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Item & Description").SetBold()));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Qty").SetBold().SetTextAlignment(TextAlignment.CENTER)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Unit Price").SetBold().SetTextAlignment(TextAlignment.RIGHT)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Amount (Kshs)").SetBold().SetTextAlignment(TextAlignment.LEFT)));

                int index = 1;
                foreach (var item in quotation.Items)
                {
                    itemTable.AddCell(new Cell().Add(new Paragraph(index.ToString()).SetTextAlignment(TextAlignment.CENTER)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Description ?? "")));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Qty?.ToString() ?? "").SetTextAlignment(TextAlignment.CENTER)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.PricePerQty.Value.ToString("N2")).SetTextAlignment(TextAlignment.RIGHT)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Amount.ToString("N2")).SetTextAlignment(TextAlignment.RIGHT)));
                    index++;
                }

                // --- TOTALS SECTION (aligned with table columns) ---
                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty #
                itemTable.AddCell(new Cell().Add(new Paragraph("TOTAL ITEMS").SetBold().SetTextAlignment(TextAlignment.LEFT)));
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.TotalItems}").SetTextAlignment(TextAlignment.RIGHT)));
                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty Qty
                itemTable.AddCell(new Cell().Add(new Paragraph("")).SetTextAlignment(TextAlignment.RIGHT)); // Empty Unit Price

                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty #
                itemTable.AddCell(new Cell().Add(new Paragraph("SUB TOTAL").SetBold().SetTextAlignment(TextAlignment.LEFT)));
                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty Qty
                itemTable.AddCell(new Cell().Add(new Paragraph("")).SetTextAlignment(TextAlignment.RIGHT)); // Empty Unit Price
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.SubTotal:N2}").SetTextAlignment(TextAlignment.RIGHT)));

                // Only add VAT row if AddVAT is true
                if (quotation.AddVAT)
                {
                    itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty #
                    itemTable.AddCell(new Cell().Add(new Paragraph("VAT (16%)").SetBold().SetTextAlignment(TextAlignment.LEFT)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty Qty
                    itemTable.AddCell(new Cell().Add(new Paragraph("")).SetTextAlignment(TextAlignment.RIGHT)); // Empty Unit Price
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.VAT:N2}").SetTextAlignment(TextAlignment.RIGHT)));
                }

                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty #
                itemTable.AddCell(new Cell().Add(new Paragraph("TOTAL AMOUNT").SetBold().SetTextAlignment(TextAlignment.LEFT)));
                itemTable.AddCell(new Cell().Add(new Paragraph(""))); // Empty Qty
                itemTable.AddCell(new Cell().Add(new Paragraph("")).SetTextAlignment(TextAlignment.RIGHT)); // Empty Unit Price
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.TotalAmount:N2}")
                    .SetBold()
                    .SetTextAlignment(TextAlignment.RIGHT)));


                document.Add(itemTable);
                document.Add(new Paragraph("\n"));

                // --- SIGNATURE & COMPANY INFO SECTION ---
                document.Add(new Paragraph("\nThis quotation remains valid for 90 days.")
                    .SetFontSize(12)
                    .SetBold()
                    .SetTextAlignment(TextAlignment.LEFT));

                document.Add(new Paragraph("Regards,")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT));

                // Signature image
                var signPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/signature.png");
                if (System.IO.File.Exists(signPath))
                {
                    var signature = new iText.Layout.Element.Image(ImageDataFactory.Create(signPath))
                        .ScaleToFit(100, 40)
                        .SetMarginTop(5)
                        .SetHorizontalAlignment(HorizontalAlignment.LEFT);
                    document.Add(signature);
                }

                // Company details (bottom info block)
                Table companyTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                    .UseAllAvailableWidth();

                document.Add(new Paragraph("Amtech Technologies LTD,")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT));

                document.Add(new Paragraph("0792716541 / 0734871556")
                    .SetFontSize(12)
                    .SetTextAlignment(TextAlignment.LEFT));

                document.Add(companyTable);


                // --- FOOTER ---
                document.ShowTextAligned(
                    new Paragraph("Amtech Plaza, Forest Line, Off Ngong Road, Matasia Shopping Center,  P. O. Box 79701 – 00200 Nairobi.\n" +
                        "Email: info@amtechafrica.com |  Web: www.amtechafrica.com |  Mobile: 0792716541/0734871556")
                        .SetFontSize(9)
                        .SetFontColor(ColorConstants.GRAY)
                        .SetFont(font),
                    PageSize.A4.GetWidth() / 2,
                    25,
                    pdf.GetNumberOfPages(),
                    TextAlignment.CENTER,
                    VerticalAlignment.BOTTOM,
                    0
                );

                document.Close();
                return File(ms.ToArray(), "application/pdf", $"Quotation_{quotation.QuotationNumber}.pdf");
            }
        }


        [Authorize]
        public async Task<IActionResult> GenerateReceiptPdf(int id)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
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
                document.Add(new Paragraph("TEL: 0792 716 541 / 0734 871 556")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("Email: info@amtechafrica.com")
                    .SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("--------------------------------------"));

                // --- RECEIPT INFO ---
                document.Add(new Paragraph($"Receipt No: {quotation.QuotationNumber ?? "-"}"));
                document.Add(new Paragraph($"Date: {quotation.DateCreated:dd/MM/yyyy}"));
                document.Add(new Paragraph($"Customer: {quotation.ClientName ?? "Walk-in"}"));
                document.Add(new Paragraph($"Phone: {quotation.PhoneNomber ?? "-"}"));
                document.Add(new Paragraph("--------------------------------------"));

                // --- ITEMS TABLE ---
                Table itemTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 })).UseAllAvailableWidth();

                itemTable.AddCell(new Cell().Add(new Paragraph("Item").SetBold()).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell().Add(new Paragraph("Qty").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell().Add(new Paragraph("Amt").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                itemTable.AddCell(new Cell(1, 3).Add(new Paragraph("--------------------------------------")).SetBorder(Border.NO_BORDER));

                foreach (var item in quotation.Items)
                {
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Description ?? "-")).SetBorder(Border.NO_BORDER));
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{item.Qty ?? 0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{item.Amount:N0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                }

                document.Add(itemTable);
                document.Add(new Paragraph("--------------------------------------"));

                // --- TOTALS ---
                document.Add(new Paragraph($"Sub Total:      {quotation.SubTotal:N0}")
                    .SetTextAlignment(TextAlignment.RIGHT));

                if (quotation.AddVAT)
                {
                    document.Add(new Paragraph($"VAT (16%):      {quotation.VAT:N0}")
                        .SetTextAlignment(TextAlignment.RIGHT));
                }

                document.Add(new Paragraph($"TOTAL:          {quotation.TotalAmount:N0}")
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

                // --- STAMP (RECEIVED) ---
                var stampText = new Paragraph()
                    .Add(new Text("AMTECH TECHNOLOGIES LTD\n")
                        .SetFontSize(9)
                        .SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text("P.O BOX 79701 - 00200, NAIROBI\n")
                        .SetFontSize(9)
                        .SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text($"DATE: {quotation.DateCreated:dd/MM/yyyy}\n")
                        .SetFontSize(9)
                        .SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text("RECEIVED\n")
                        .SetFontSize(18)
                        .SetBold()
                        .SetFontColor(new DeviceRgb(0, 102, 204)));

                // --- Centered Stamp ---
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

                document.Close();
                return File(ms.ToArray(), "application/pdf", $"Receipt_{quotation.QuotationNumber}.pdf");
            }
        }



        [Authorize]
        public async Task<IActionResult> GeneratePdfWithReceipt(int id)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Items)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
                return NotFound();

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4);
                document.SetMargins(40, 40, 60, 40);

                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                document.SetFont(font).SetFontSize(12);

                // --- LOGO ---
                var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/amtech_logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    var logo = new iText.Layout.Element.Image(ImageDataFactory.Create(logoPath))
                        .ScaleToFit(220, 80)
                        .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                        .SetMarginBottom(5);
                    document.Add(logo);
                }

                // --- TITLE & QUOTATION NUMBER ---
                Table titleTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();
                titleTable.AddCell(new Cell().Add(new Paragraph("QUOTATION").SetFontSize(16).SetBold().SetTextAlignment(TextAlignment.LEFT)).SetBorder(Border.NO_BORDER));
                titleTable.AddCell(new Cell().Add(new Paragraph($"Quotation No: {quotation.QuotationNumber}").SetFontSize(12).SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                document.Add(titleTable);

                // --- ITEM TABLE ---
                Table itemTable = new Table(new float[] { 0.6f, 4.5f, 1f, 2f, 2f }).UseAllAvailableWidth();
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("#").SetBold().SetTextAlignment(TextAlignment.CENTER)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Item & Description").SetBold()));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Qty").SetBold().SetTextAlignment(TextAlignment.CENTER)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Unit Price").SetBold().SetTextAlignment(TextAlignment.RIGHT)));
                itemTable.AddHeaderCell(new Cell().Add(new Paragraph("Amount (Kshs)").SetBold().SetTextAlignment(TextAlignment.RIGHT)));

                int index = 1;
                foreach (var item in quotation.Items)
                {
                    itemTable.AddCell(new Cell().Add(new Paragraph(index.ToString()).SetTextAlignment(TextAlignment.CENTER)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Description ?? "")));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Qty?.ToString() ?? "").SetTextAlignment(TextAlignment.CENTER)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.PricePerQty.Value.ToString("N2")).SetTextAlignment(TextAlignment.RIGHT)));
                    itemTable.AddCell(new Cell().Add(new Paragraph(item.Amount.ToString("N2")).SetTextAlignment(TextAlignment.RIGHT)));
                    index++;
                }

                // --- TOTALS ---
                itemTable.AddCell(new Cell(1, 3).Add(new Paragraph("TOTAL ITEMS").SetBold()));
                itemTable.AddCell(new Cell().Add(new Paragraph("")));
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.TotalItems}").SetTextAlignment(TextAlignment.RIGHT)));

                itemTable.AddCell(new Cell(1, 4).Add(new Paragraph("SUB TOTAL").SetBold()));
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.SubTotal:N2}").SetTextAlignment(TextAlignment.RIGHT)));

                if (quotation.AddVAT)
                {
                    itemTable.AddCell(new Cell(1, 4).Add(new Paragraph("VAT (16%)").SetBold()));
                    itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.VAT:N2}").SetTextAlignment(TextAlignment.RIGHT)));
                }

                itemTable.AddCell(new Cell(1, 4).Add(new Paragraph("TOTAL AMOUNT").SetBold()));
                itemTable.AddCell(new Cell().Add(new Paragraph($"{quotation.TotalAmount:N2}").SetBold().SetTextAlignment(TextAlignment.RIGHT)));
                document.Add(itemTable);
                document.Add(new Paragraph("\n"));

                // --- VALIDITY + SIGNATURE ---
                document.Add(new Paragraph("This quotation remains valid for 90 days.").SetFontSize(12).SetBold());
                document.Add(new Paragraph("Regards,").SetFontSize(12));

                var signPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/signature.png");
                if (System.IO.File.Exists(signPath))
                {
                    var signature = new iText.Layout.Element.Image(ImageDataFactory.Create(signPath))
                        .ScaleToFit(100, 40)
                        .SetMarginTop(5)
                        .SetHorizontalAlignment(HorizontalAlignment.LEFT);
                    document.Add(signature);
                }

                document.Add(new Paragraph($"{quotation.ClientName ?? ""}"));
                document.Add(new Paragraph($"{quotation.PhoneNomber ?? ""}"));

                // --- FOOTER ---
                document.ShowTextAligned(
                    new Paragraph("Amtech Plaza, Forest Line, Off Ngong Road, Matasia Shopping Center,  P. O. Box 79701 – 00200 Nairobi.\n" +
                        "Email: info@amtechafrica.com |  Web: www.amtechafrica.com |  Mobile: 0792716541/0734871556")
                        .SetFontSize(9)
                        .SetFontColor(ColorConstants.GRAY),
                    PageSize.A4.GetWidth() / 2,
                    25,
                    pdf.GetNumberOfPages(),
                    TextAlignment.CENTER,
                    VerticalAlignment.BOTTOM,
                    0
                );

                // ✅ ADD RECEIPT ON NEXT PAGE
                pdf.AddNewPage();
                document.SetMargins(20, 20, 20, 20);
                document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));

                PdfFont receiptFont = PdfFontFactory.CreateFont(StandardFonts.COURIER);
                document.SetFont(receiptFont).SetFontSize(9);

                // --- HEADER ---
                document.Add(new Paragraph("AMTECH TECHNOLOGIES LTD").SetTextAlignment(TextAlignment.CENTER).SetBold().SetFontSize(10));
                document.Add(new Paragraph("P.O BOX 79701 - 00200, NAIROBI").SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("TEL: 0792 716 541 / 0734 871 556").SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("Email: info@amtechafrica.com").SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("--------------------------------------"));

                document.Add(new Paragraph($"Receipt No: {quotation.QuotationNumber ?? "-"}"));
                document.Add(new Paragraph($"Date: {quotation.DateCreated:dd/MM/yyyy}"));
                document.Add(new Paragraph($"Customer: {quotation.ClientName ?? "Walk-in"}"));
                document.Add(new Paragraph($"Phone: {quotation.PhoneNomber ?? "-"}"));
                document.Add(new Paragraph("--------------------------------------"));

                Table receiptTable = new Table(UnitValue.CreatePercentArray(new float[] { 2, 1, 1 })).UseAllAvailableWidth();
                receiptTable.AddCell(new Cell().Add(new Paragraph("Item").SetBold()).SetBorder(Border.NO_BORDER));
                receiptTable.AddCell(new Cell().Add(new Paragraph("Qty").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                receiptTable.AddCell(new Cell().Add(new Paragraph("Amt").SetBold()).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));
                receiptTable.AddCell(new Cell(1, 3).Add(new Paragraph("--------------------------------------")).SetBorder(Border.NO_BORDER));

                foreach (var item in quotation.Items)
                {
                    receiptTable.AddCell(new Cell().Add(new Paragraph(item.Description ?? "-")).SetBorder(Border.NO_BORDER));
                    receiptTable.AddCell(new Cell().Add(new Paragraph($"{item.Qty ?? 0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                    receiptTable.AddCell(new Cell().Add(new Paragraph($"{item.Amount:N0}").SetTextAlignment(TextAlignment.RIGHT)).SetBorder(Border.NO_BORDER));
                }

                document.Add(receiptTable);
                document.Add(new Paragraph("--------------------------------------"));

                document.Add(new Paragraph($"Sub Total:      {quotation.SubTotal:N0}").SetTextAlignment(TextAlignment.RIGHT));
                if (quotation.AddVAT)
                {
                    document.Add(new Paragraph($"VAT (16%):      {quotation.VAT:N0}").SetTextAlignment(TextAlignment.RIGHT));
                }
                document.Add(new Paragraph($"TOTAL:          {quotation.TotalAmount:N0}").SetTextAlignment(TextAlignment.RIGHT).SetBold());
                document.Add(new Paragraph("--------------------------------------"));

                document.Add(new Paragraph("UNDERSTANDING YOUR BUSINESS BETTER!").SetTextAlignment(TextAlignment.CENTER).SetFontSize(9).SetBold());
                document.Add(new Paragraph("Visit Again").SetTextAlignment(TextAlignment.CENTER));
                document.Add(new Paragraph("--------------------------------------"));
                document.Add(new Paragraph("Powered by Amtech Africa").SetTextAlignment(TextAlignment.CENTER).SetFontSize(8).SetFontColor(ColorConstants.GRAY));

                // ✅ STAMP (CENTERED)
                var stampText = new Paragraph()
                    .Add(new Text("AMTECH TECHNOLOGIES LTD\n").SetFontSize(9).SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text("P.O BOX 79701 - 00200, NAIROBI\n").SetFontSize(9).SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text($"DATE: {quotation.DateCreated:dd/MM/yyyy}\n").SetFontSize(9).SetFontColor(new DeviceRgb(0, 102, 204)))
                    .Add(new Text("RECEIVED\n").SetFontSize(18).SetBold().SetFontColor(new DeviceRgb(0, 102, 204)));

                // --- Centered on the receipt page ---
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
                    (float)(Math.PI / 6)
                );

                document.Close();

                return File(ms.ToArray(), "application/pdf", $"Quotation_Receipt_{quotation.QuotationNumber}.pdf");
            }
        }
        
    }
}
