using DocumentFormat.OpenXml.Packaging;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.BatchAI.Fluent.Models;
using Microsoft.Bot.Connector;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf.IO;
using Reports.Data;
using Reports.Models;
using ServiceStack.Text;
using System.Net.Mail;
using System.Security.Claims;
using Path = System.IO.Path;
using PdfReader = iText.Kernel.Pdf.PdfReader;

namespace Reports.Controllers
{
    public class PaymentVouchersController : Controller
    {
        private readonly ReportsDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PdfImageXObject imgData { get; private set; }

        public PaymentVouchersController(ReportsDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: PaymentVouchers
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var vouchers = await _context.PaymentVouchers
                .Include(v => v.Items)
                .ToListAsync();
            return View(vouchers);
        }

        //In words
        public static class NumberToWordsConverter
        {
            private static readonly string[] UnitsMap =
            { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
      "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };

            private static readonly string[] TensMap =
            { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            public static string ConvertAmountToWords(decimal amount)
            {
                long integerPart = (long)Math.Floor(amount);
                int cents = (int)((amount - integerPart) * 100);

                string words = NumberToWords(integerPart);

                if (cents > 0)
                {
                    words += $" and {cents}/100";
                }

                return words + " Only";
            }

            private static string NumberToWords(long number)
            {
                if (number == 0)
                    return "Zero";

                if (number < 0)
                    return "Minus " + NumberToWords(Math.Abs(number));

                var words = "";

                if ((number / 1000000) > 0)
                {
                    words += NumberToWords(number / 1000000) + " Million ";
                    number %= 1000000;
                }

                if ((number / 1000) > 0)
                {
                    words += NumberToWords(number / 1000) + " Thousand ";
                    number %= 1000;
                }

                if ((number / 100) > 0)
                {
                    words += NumberToWords(number / 100) + " Hundred ";
                    number %= 100;
                }

                if (number > 0)
                {
                    if (words != "")
                        words += "and ";

                    if (number < 20)
                        words += UnitsMap[number];
                    else
                    {
                        words += TensMap[number / 10];
                        if ((number % 10) > 0)
                            words += "-" + UnitsMap[number % 10];
                    }
                }

                return words.Trim();
            }
        }        

        // GET: PaymentVouchers/Create
        [Authorize]
        public IActionResult Create()
        {
            return View(new PaymentVoucher
            {
                Date = DateTime.Now,
                VoucherNumber = $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}",
                ChequeNo = "B2C"
            });
        }

        // POST: PaymentVouchers/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentVoucher voucher)
        {
            voucher.Date = DateTime.Now;
            voucher.VoucherNumber = $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}";
            voucher.ChequeNo = "B2C";
            //voucher.ChequeNo = $"CHQ-{DateTime.Now:yyyyMMddHHmmssfff}";


            // If items are null, initialize an empty list to prevent errors
            if (voucher.Items == null)
                voucher.Items = new List<PaymentVoucherItem>();

            // Force at least one item if none provided (optional)
            if (!voucher.Items.Any())
            {
                voucher.Items.Add(new PaymentVoucherItem
                {
                    Description = "Default Item",
                    Amount = 0
                });
            }

            // Calculate total amount safely
            voucher.TotalAmount = voucher.Items.Sum(i => i.Amount);

            // Auto-generate amount in words
            voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

            // **Set logged-in user**
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            voucher.CreatedById = int.Parse(userId);

            // Force the model state to be valid (even if errors exist)
            ModelState.Clear();
            TryValidateModel(voucher); // Revalidate after adjustments


            if (!ModelState.IsValid)
            {
                // Force saving despite errors
                _context.PaymentVouchers.Add(voucher);
                await _context.SaveChangesAsync();
                
                return RedirectToAction(nameof(Index));
            }

            // Normal save if everything is fine
            _context.PaymentVouchers.Add(voucher);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


        // GET: PaymentVouchers/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .FirstOrDefaultAsync(m => m.PaymentVoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        // POST: PaymentVouchers/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PaymentVoucher voucher)
        {
            if (id != voucher.PaymentVoucherId)
                return BadRequest("Voucher ID mismatch between route and model.");

            // Ensure Items is not null
            voucher.Items ??= new List<PaymentVoucherItem>();

            // 🟢 Preserve or regenerate system fields (like in Create)
            if (voucher.Date == default)
                voucher.Date = DateTime.Now;

            voucher.VoucherNumber ??= $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}";
            voucher.ChequeNo ??= "B2C";
            //voucher.ChequeNo ??= $"CHQ-{DateTime.Now:yyyyMMddHHmmssfff}";

            // Attach items to parent
            foreach (var item in voucher.Items)
            {
                item.PaymentVoucherId = voucher.PaymentVoucherId          ;
                item.PaymentVoucher = voucher;
            }

            // Recalculate totals
            voucher.TotalAmount = voucher.Items.Sum(i => i.Amount);
            voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

            // **Set logged-in user automatically**
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            voucher.CreatedById = int.Parse(userId);

            ModelState.Clear();
            TryValidateModel(voucher);

            if (!ModelState.IsValid)
            {
                // DEBUG
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        Console.WriteLine($"[MODEL ERROR] {kvp.Key} => {error.ErrorMessage}");
                    }
                }
                return View(voucher);
            }

            try
            {
                _context.Update(voucher);

                var existingItems = _context.PaymentVoucherItems
                    .Where(i => i.PaymentVoucherId == voucher.PaymentVoucherId)
                    .ToList();

                // Remove deleted items
                foreach (var existing in existingItems)
                {
                    if (!voucher.Items.Any(i => i.PaymentVoucherId == existing.PaymentVoucherId))
                        _context.PaymentVoucherItems.Remove(existing);
                }

                // Add or update items
                foreach (var item in voucher.Items)
                {
                    if (item.PaymentVoucherId == 0)
                        _context.PaymentVoucherItems.Add(item);
                    else
                        _context.PaymentVoucherItems.Update(item);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentVoucherExists(voucher.PaymentVoucherId))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }
        private bool PaymentVoucherExists(int id)
        {
            throw new NotImplementedException();
        }


        // GET: PaymentVouchers/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher == null) return NotFound();
            return View(voucher);
        }

        // POST: PaymentVouchers/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher != null)
            {
                _context.PaymentVoucherItems.RemoveRange(voucher.Items);
                _context.PaymentVouchers.Remove(voucher);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: PaymentVouchers/Details/5
        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items) // Include voucher items
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            return View(voucher);
        }

        //==========================================
        //Access reports outsiide index
        //==========================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Reports(string voucherName = null)
        {
            IQueryable<PaymentVoucher> query = _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.UploadedFile)
                .Include(v => v.Summaries)
                    .ThenInclude(s => s.Venue)
                .Include(v => v.Summaries)
                    .ThenInclude(s => s.County);

            // If voucherName is supplied, filter by it
            if (!string.IsNullOrEmpty(voucherName))
            {
                query = query.Where(v => v.VoucheName == voucherName);
            }

            var vouchers = await query.ToListAsync();

            return View(vouchers);
        }

        [Authorize]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher == null)
                return NotFound();

            using (var ms = new MemoryStream())
            {
                var writer = new PdfWriter(ms);
                var pdf = new PdfDocument(writer);
                PdfFont times = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);

                var document = new Document(pdf, PageSize.A4);
                document.SetFont(times);

                // --- Amtech Logo ---
                var imgPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/amtech_logo.png");
                if (System.IO.File.Exists(imgPath))
                {
                    var imgData = ImageDataFactory.Create(imgPath);
                    var logo = new iText.Layout.Element.Image(imgData)
                        .ScaleToFit(150, 70)
                        .SetHorizontalAlignment(HorizontalAlignment.CENTER);
                    document.Add(logo);
                }

                // --- Title ---
                var title = new Paragraph("Cheque/Cash Payment Voucher")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(16)
                    .SetUnderline(0.5f, -1);
                document.Add(title);

                // --- Payment Options ---
                string mpesaBox = voucher.PaymentMode == "M-pesa" ? "[X]" : "[ ]";
                string cashBox = voucher.PaymentMode == "Cash" ? "[X]" : "[ ]";

                document.Add(new Paragraph($"M-pesa {mpesaBox}   Cash {cashBox}")
                    .SetTextAlignment(TextAlignment.LEFT)
                    .SetFontSize(10));

                // --- Voucher Details ---
                document.Add(new Paragraph()
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .Add($"Voucher: {voucher.VoucherNumber}\n")
                    .Add($"Cheque No: {voucher.ChequeNo}\n")
                    .Add($"Date: {voucher.Date:dd/MM/yyyy}"));

                // --- Payment in favour of ---
                document.Add(new Paragraph("Payment in Favour of: ")
                    .SetFontSize(11)
                    .Add(new Text(voucher.PaymentInFavourOf ?? "").SetUnderline(0.5f, -1)));

                // --- Items Table ---
                var table = new Table(new float[] { 1, 6, 2, 1 }).UseAllAvailableWidth();
                table.AddHeaderCell("No");
                table.AddHeaderCell("Description");
                table.AddHeaderCell("Amount");
                table.AddHeaderCell("Cts");

                int i = 1;
                foreach (var item in voucher.Items)
                {
                    table.AddCell(i.ToString());
                    table.AddCell(item.Description);
                    table.AddCell(((int)item.Amount).ToString());
                    table.AddCell(((int)((item.Amount - Math.Truncate(item.Amount)) * 100)).ToString("00"));
                    i++;
                }

                table.AddCell(new Cell(1, 2).Add(new Paragraph("TOTAL")));
                table.AddCell(new Cell().Add(new Paragraph(((int)voucher.TotalAmount).ToString())));
                table.AddCell(new Cell().Add(new Paragraph(
                    ((int)((voucher.TotalAmount - Math.Truncate((decimal)voucher.TotalAmount)) * 100)).ToString("00")
                )));
                document.Add(table);

                // --- Amount in words ---
                document.Add(new Paragraph("\nAmount in words: ")
                    .SetFontSize(11)
                    .Add(new Text(voucher.AmountInWords ?? "").SetUnderline(0.5f, -1)));

                // --- Payment Made in ---
                string paymentText = (!string.IsNullOrEmpty(voucher.PaymentMode) &&
                    voucher.PaymentMode.Equals("M-pesa", StringComparison.OrdinalIgnoreCase))
                    ? "Ksh B2C"
                    : "Ksh (Cash)";

                document.Add(new Paragraph("\nPayment Made in: ")
                    .SetFontSize(11)
                    .Add(new Text(paymentText).SetUnderline(0.5f, -1)));

                // --- Signature Section ---
                document.Add(new Paragraph("\nPrepared By: ")
                            .Add(new Text(voucher.PreparedBy ?? "").SetUnderline())
                            .Add("   Sign: ")
                            .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline()));

                document.Add(new Paragraph("Checked By: ")
                    .Add(new Text(voucher.CheckedBy ?? "").SetUnderline())
                    .Add("   Sign: ")
                    .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline()));

                document.Add(new Paragraph("Approved By: ")
                    .Add(new Text(voucher.ApprovedBy ?? "").SetUnderline())
                    .Add("   Sign: ")
                    .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline()));

                // --- Footer (ONLY for this page) ---
                var footerText = "Vinodeep Towers, 3rd Floor, Suite 305, Baricho Road, Off Bunyala Road\n" +
                                 "P. O. Box 79701 – 00200 Nairobi. Direct line +254 20 553300, Mobile: 0734871556, Fax +254 20 559867\n" +
                                 "Email: info@amtechafrica.com   Web: www.amtechafrica.com";

                document.ShowTextAligned(
                    new Paragraph(footerText).SetFontSize(9).SetTextAlignment(TextAlignment.CENTER),
                    PageSize.A4.GetWidth() / 2,
                    30, // distance from bottom
                    TextAlignment.CENTER);

                document.Close();

                return File(ms.ToArray(), "application/pdf", $"Voucher_{voucher.VoucherNumber}.pdf");
            }
        }
[HttpGet]
[Authorize]
public async Task<IActionResult> GenerateFullReport(int id)
{
    var voucher = await _context.PaymentVouchers
        .Include(v => v.Items)
        .Include(v => v.UploadedFile)   // ✅ include uploaded files
        .Include(v => v.Summaries)
            .ThenInclude(s => s.Venue)
        .Include(v => v.Summaries)
            .ThenInclude(s => s.County)
        .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

    if (voucher == null)
        return NotFound();

    using var ms = new MemoryStream();
    using var writer = new PdfWriter(ms);
    using var pdf = new PdfDocument(writer);
    var doc = new Document(pdf, PageSize.A4, false);

    PdfFont times = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
    doc.SetFont(times);

    // --- Logo ---
    var imgPath = Path.Combine(_env.WebRootPath, "images", "amtech_logo.png");
    if (System.IO.File.Exists(imgPath))
    {
        var imgData = ImageDataFactory.Create(imgPath);
        var logo = new iText.Layout.Element.Image(imgData)
            .ScaleToFit(150, 70)
            .SetHorizontalAlignment(HorizontalAlignment.CENTER)
            .SetMarginBottom(10);
        doc.Add(logo);
    }

    // --- Voucher Header ---
    var title = new Paragraph("Cheque/Cash Payment Voucher")
        .SetTextAlignment(TextAlignment.CENTER)
        .SetFontSize(16)
        .SetUnderline(0.5f, -1)
        .SetMarginBottom(10);
    doc.Add(title);

    string mpesaBox = voucher.PaymentMode == "M-pesa" ? "[X]" : "[ ]";
    string cashBox = voucher.PaymentMode == "Cash" ? "[X]" : "[ ]";
    doc.Add(new Paragraph($"M-pesa {mpesaBox}   Cash {cashBox}")
        .SetFontSize(10)
        .SetMarginBottom(5));

    doc.Add(new Paragraph()
        .SetTextAlignment(TextAlignment.RIGHT)
        .Add($"Voucher: {voucher.VoucherNumber}\n")
        .Add($"Cheque No: {voucher.ChequeNo}\n")
        .Add($"Date: {voucher.Date:dd/MM/yyyy}")
        .SetMarginBottom(10));

    doc.Add(new Paragraph("Payment in Favour of: ")
        .SetFontSize(11)
        .Add(new Text(voucher.PaymentInFavourOf ?? "").SetUnderline(0.5f, -1))
        .SetMarginBottom(15));

    // --- Items Table ---
    var table = new Table(new float[] { 1, 6, 2, 1 }).UseAllAvailableWidth();
    table.AddHeaderCell("No");
    table.AddHeaderCell("Description");
    table.AddHeaderCell("Amount");
    table.AddHeaderCell("Cts");

    int i = 1;
    foreach (var item in voucher.Items)
    {
        table.AddCell(i.ToString());
        table.AddCell(item.Description);
        table.AddCell(((int)item.Amount).ToString());
        table.AddCell(((int)((item.Amount - Math.Truncate(item.Amount)) * 100)).ToString("00"));
        i++;
    }

    table.AddCell(new Cell(1, 2).Add(new Paragraph("TOTAL")));
    table.AddCell(new Cell().Add(new Paragraph(((int)voucher.TotalAmount).ToString())));
    table.AddCell(new Cell().Add(new Paragraph(
        ((int)((voucher.TotalAmount - Math.Truncate((decimal)voucher.TotalAmount)) * 100)).ToString("00")
    )));
    table.SetMarginBottom(15);
    doc.Add(table);

    doc.Add(new Paragraph("\nAmount in words: ")
        .SetFontSize(11)
        .Add(new Text(voucher.AmountInWords ?? "").SetUnderline(0.5f, -1))
        .SetMarginBottom(0));

    // --- Payment Made in ---
    string paymentText = (!string.IsNullOrEmpty(voucher.PaymentMode) &&
        voucher.PaymentMode.Equals("M-pesa", StringComparison.OrdinalIgnoreCase))
        ? "Ksh B2C"
        : "Ksh (Cash)";

    doc.Add(new Paragraph("\nPayment Made in: ")
        .SetFontSize(11)
        .Add(new Text(paymentText).SetUnderline(0.5f, -1)));

    // --- Signature Section ---
    doc.Add(new Paragraph("\nPrepared By: ")
        .Add(new Text(voucher.PreparedBy ?? "").SetUnderline(0.5f, -1))
        .Add("   Sign: ")
        .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline(0.5f, -1)));

    doc.Add(new Paragraph("Checked By: ")
        .Add(new Text(voucher.CheckedBy ?? "").SetUnderline(0.5f, -1))
        .Add("   Sign: ")
        .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline(0.5f, -1)));

    doc.Add(new Paragraph("Approved By: ")
        .Add(new Text(voucher.ApprovedBy ?? "").SetUnderline(0.5f, -1))
        .Add("   Sign: ")
        .Add(new Text("XXXXXXXXXXXXXXX").SetUnderline(0.5f, -1)));

    // --- Footer ---
    var footerText = "Vinodeep Towers, 3rd Floor, Suite 305, Baricho Road, Off Bunyala Road\n" +
                     "P. O. Box 79701 – 00200 Nairobi. Direct line +254 20 553300, Mobile: 0734871556, Fax +254 20 559867\n" +
                     "Email: info@amtechafrica.com   Web: www.amtechafrica.com";

    doc.ShowTextAligned(
        new Paragraph(footerText).SetFontSize(9).SetTextAlignment(TextAlignment.CENTER),
        PageSize.A4.GetWidth() / 2,
        30,
        TextAlignment.CENTER);

    // --- Summaries Section ---
    if (voucher.Summaries.Any())
    {
        doc.Add(new AreaBreak()); // new page for summaries
        decimal grandTotal = 0;

        foreach (var summary in voucher.Summaries)
        {
            // ===== VENUE HEADER =====
            Table venueTable = new Table(new float[] { 1, 3, 3 })
                .UseAllAvailableWidth()
                .SetFontSize(10);

            venueTable.AddCell(new Cell().Add(new Paragraph("VENUE")));
            venueTable.AddCell(new Cell(1, 2).Add(new Paragraph(summary.Venue?.Name ?? "")));
            doc.Add(venueTable);

            // ===== MAIN TABLE =====
            Table summaryTable = new Table(new float[] { 3, 1, 1, 2, 2 })
                .UseAllAvailableWidth()
                .SetFontSize(10);

            // Hall Hire
            summaryTable.AddCell("HALL HIRE");
            summaryTable.AddCell(summary.HallHireNo.ToString());
            summaryTable.AddCell(summary.EachHallAmount.ToString("N0"));
            summaryTable.AddCell(summary.HallHireTotal.ToString("N2"));
            summaryTable.AddCell("PAID");

            summaryTable.AddCell("MPESA CHARGES");
            summaryTable.AddCell("1");
            summaryTable.AddCell(summary.MpesaChargesPerHall.ToString("N0"));
            summaryTable.AddCell((summary.HallHireNo * summary.MpesaChargesPerHall).ToString("N2"));
            summaryTable.AddCell("PAID");

            // Lead Farmers
            summaryTable.AddCell("LEAD FARMERS");
            summaryTable.AddCell(summary.LeadFarmersNo.ToString());
            summaryTable.AddCell(summary.EachLeadFarmersAmount.ToString("N0"));
            summaryTable.AddCell((summary.LeadFarmersNo * summary.EachLeadFarmersAmount).ToString("N2"));
            summaryTable.AddCell("");

            summaryTable.AddCell("MPESA CHARGES");
            summaryTable.AddCell(summary.LeadFarmersNo.ToString());
            summaryTable.AddCell(summary.MpesaChargesPerLeadFarmers.ToString("N0"));
            summaryTable.AddCell((summary.LeadFarmersNo * summary.MpesaChargesPerLeadFarmers).ToString("N2"));
            summaryTable.AddCell("");

            // EA
            summaryTable.AddCell("E.A");
            summaryTable.AddCell(summary.EANo.ToString());
            summaryTable.AddCell(summary.EachEAAmount.ToString("N0"));
            summaryTable.AddCell((summary.EANo * summary.EachEAAmount).ToString("N2"));
            summaryTable.AddCell("");

            summaryTable.AddCell("MPESA CHARGES");
            summaryTable.AddCell(summary.EANo.ToString());
            summaryTable.AddCell(summary.MpesaChargesPerEA.ToString("N0"));
            summaryTable.AddCell((summary.EANo * summary.MpesaChargesPerEA).ToString("N2"));
            summaryTable.AddCell("");

            // Subtotal
            summaryTable.AddCell(new Cell(1, 4).Add(new Paragraph("sub total")));
            summaryTable.AddCell(new Paragraph(summary.SubTotalperVenue.ToString("N2")));

            doc.Add(summaryTable);
            grandTotal += summary.SubTotalperVenue;
        }

        // ===== GRAND TOTAL =====
        Table sumTable = new Table(new float[] { 1, 3, 3 })
            .UseAllAvailableWidth()
            .SetFontSize(10);

        sumTable.AddCell("SUM");
        sumTable.AddCell(new Cell(1, 2).Add(new Paragraph(grandTotal.ToString("N2"))));
        doc.Add(sumTable);
    }

    // --- Uploaded Files Section (Batch Files) ---
    if (voucher.UploadedFile.Any())
    {
        doc.Add(new AreaBreak());
        var header = new Paragraph("Attached Batch Files")
            .SetFontSize(14)
            .SetTextAlignment(TextAlignment.CENTER)
            .SetMarginBottom(15);
        doc.Add(header);

        foreach (var file in voucher.UploadedFile.OrderBy(f => f.UploadedOn))
        {
            var physicalPath = Path.Combine(_env.WebRootPath, file.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(physicalPath)) continue;

            var ext = Path.GetExtension(physicalPath).ToLowerInvariant();

            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                var imgData = ImageDataFactory.Create(physicalPath);
                var img = new iText.Layout.Element.Image(imgData)
                    .ScaleToFit(PageSize.A4.GetWidth() - 40, PageSize.A4.GetHeight() - 40)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER);
                doc.Add(img);
            }
            else if (ext == ".pdf")
            {
                using var reader = new PdfReader(physicalPath);
                using var inputPdf = new PdfDocument(reader);
                inputPdf.CopyPagesTo(1, inputPdf.GetNumberOfPages(), pdf);
            }
            else if (ext == ".docx")
            {
                string text = ExtractTextFromDocx(physicalPath);
                doc.Add(new Paragraph(text).SetFontSize(11));
                doc.Add(new AreaBreak());
            }
            else if (ext == ".txt")
            {
                string text = System.IO.File.ReadAllText(physicalPath);
                doc.Add(new Paragraph(text).SetFontSize(11));
                doc.Add(new AreaBreak());
            }
        }
    }

    doc.Close();
    return File(ms.ToArray(), "application/pdf", $"Voucher_{voucher.VoucherNumber}_FullReport.pdf");
    }
        
        // ===== Helper for DOCX text =====
        private string ExtractTextFromDocx(string path)
        {
            try
            {
                using var wordDoc = WordprocessingDocument.Open(path, false);
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                return body?.InnerText ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
