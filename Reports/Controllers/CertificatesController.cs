using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Reports.Data;
using Reports.Models;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace Reports.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly ReportsDbContext _context;

        public CertificatesController(ReportsDbContext context)
        {
            _context = context;
        }

        // GET: Certificates (Combined Index/Create page)
        [Authorize]
        public async Task<IActionResult> Index()
        {
            ViewBag.Certificates = await _context.Certificates.ToListAsync();
            return View(new Certificate());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CertTitle,RecipientName,TrainingTitle,Location,EventDate,IssueDate,CertificateNumber,CompanyName,CEO_Name,CEO_Title,Trainer_Name,Trainer_Title")] Certificate certificate,
                                         IFormFile? CEOSignatureFile, IFormFile? TrainerSignatureFile)
        {
            if (ModelState.IsValid)
            {
                // Generate certificate number if not provided
                if (string.IsNullOrEmpty(certificate.CertificateNumber))
                {
                    certificate.CertificateNumber = "CERT-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                }

                // Save CEO signature
                if (CEOSignatureFile != null && CEOSignatureFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await CEOSignatureFile.CopyToAsync(ms);
                    certificate.CEOSignature = ms.ToArray();
                }

                // Save Trainer signature
                if (TrainerSignatureFile != null && TrainerSignatureFile.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await TrainerSignatureFile.CopyToAsync(ms);
                    certificate.TrainerSignature = ms.ToArray();
                }

                _context.Add(certificate);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Certificate created successfully!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = "Please fill in all required fields.";
            ViewBag.Certificates = await _context.Certificates.ToListAsync();
            return View("Index", certificate);
        }

        // GET: Certificates/GetCertificate/5 (AJAX endpoint)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetCertificate(int id)
        {
            try
            {
                var certificate = await _context.Certificates.FindAsync(id);
                if (certificate == null)
                {
                    return NotFound(new { message = "Certificate not found" });
                }

                // Return the data with camelCase property names for JavaScript
                var result = new
                {
                    id = certificate.Id,
                    recipientName = certificate.RecipientName ?? "",
                    trainingTitle = certificate.TrainingTitle ?? "",
                    location = certificate.Location ?? "",
                    eventDate = certificate.EventDate.ToString("yyyy-MM-dd"),
                    issueDate = certificate.IssueDate.ToString("yyyy-MM-dd"),
                    certificateNumber = certificate.CertificateNumber ?? "",
                    ceo_Name = certificate.CEO_Name ?? "",
                    ceo_Title = certificate.CEO_Title ?? "",
                    trainer_Name = certificate.Trainer_Name ?? "",
                    trainer_Title = certificate.Trainer_Title ?? ""
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                // Log the error to console or to your logging system
                Console.WriteLine($"Error in GetCertificate: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new { message = "Error loading certificate details", error = ex.Message });
            }
        }

        // POST: Certificates/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,CertTitle,RecipientName,TrainingTitle,Location,EventDate,IssueDate,CertificateNumber,CompanyName,CEO_Name,CEO_Title,Trainer_Name,Trainer_Title")] Certificate certificate,
            IFormFile? CEOSignatureFile,
            IFormFile? TrainerSignatureFile
        )
        {
            if (id != certificate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Load existing certificate from DB
                    var existingCert = await _context.Certificates.FindAsync(id);
                    if (existingCert == null)
                        return NotFound();

                    // Update fields
                    existingCert.RecipientName = certificate.RecipientName;
                    existingCert.TrainingTitle = certificate.TrainingTitle;
                    existingCert.Location = certificate.Location;
                    existingCert.EventDate = certificate.EventDate;
                    existingCert.IssueDate = certificate.IssueDate;
                    existingCert.CertificateNumber = certificate.CertificateNumber;
                    existingCert.CEO_Name = certificate.CEO_Name;
                    existingCert.CEO_Title = certificate.CEO_Title;
                    existingCert.Trainer_Name = certificate.Trainer_Name;
                    existingCert.Trainer_Title = certificate.Trainer_Title;

                    // Update CEO signature if a new file is uploaded
                    if (CEOSignatureFile != null && CEOSignatureFile.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await CEOSignatureFile.CopyToAsync(ms);
                        existingCert.CEOSignature = ms.ToArray();
                    }

                    // Update Trainer signature if a new file is uploaded
                    if (TrainerSignatureFile != null && TrainerSignatureFile.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await TrainerSignatureFile.CopyToAsync(ms);
                        existingCert.TrainerSignature = ms.ToArray();
                    }

                    _context.Update(existingCert);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Certificate updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CertificateExists(certificate.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Certificates = await _context.Certificates.ToListAsync();
            TempData["Error"] = "Error updating certificate.";
            return View("Index", certificate);
        }

        // POST: Certificates/Delete/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var certificate = await _context.Certificates.FindAsync(id);
            if (certificate != null)
            {
                _context.Certificates.Remove(certificate);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Certificate deleted successfully!";
            }
            else
            {
                TempData["Error"] = "Certificate not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CertificateExists(int id)
        {
            return _context.Certificates.Any(e => e.Id == id);
        }


        // GET: Certificates/Generate/5
        [Authorize]
        public async Task<IActionResult> Generate(int id)
        {
            var certificate = await _context.Certificates.FindAsync(id);
            if (certificate == null)
            {
                return NotFound();
            }

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    var pageSize = iTextSharp.text.PageSize.A4.Rotate();
                    var document = new iTextSharp.text.Document(pageSize, 0, 0, 0, 0);
                    var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, stream);
                    document.Open();

                    var canvas = writer.DirectContent;
                    float centerX = pageSize.Width / 2;

                    // ================= BACKGROUND =================
                    string bgImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "BackGround.png");
                    if (System.IO.File.Exists(bgImagePath))
                    {
                        var bg = iTextSharp.text.Image.GetInstance(bgImagePath);
                        bg.ScaleAbsolute(pageSize.Width, pageSize.Height);
                        bg.SetAbsolutePosition(0, 0);
                        document.Add(bg);
                    }

                    // ================= LOGO (VISIBLE & WELL POSITIONED) =================
                    string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "AmtechCert.png");
                    if (System.IO.File.Exists(logoPath))
                    {
                        var logo = iTextSharp.text.Image.GetInstance(logoPath);
                        logo.ScaleToFit(120f, 70f);
                        logo.SetAbsolutePosition(centerX - 50, pageSize.Height - 100);
                        document.Add(logo);
                    }

                    // ================= FONTS =================
                    var titleFont = FontFactory.GetFont("Helvetica", 28, iTextSharp.text.Font.BOLD, BaseColor.RED);
                    var subFont = FontFactory.GetFont("Helvetica", 20, iTextSharp.text.Font.NORMAL);
                    var normalFont = FontFactory.GetFont("Helvetica", 18, iTextSharp.text.Font.NORMAL);
                    var smallFont = FontFactory.GetFont("Helvetica", 10, iTextSharp.text.Font.NORMAL);

                    // ✅ SCRIPT FONT WITH FALLBACK (NO ERROR)
                    iTextSharp.text.Font nameFont;
                    string fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "GreatVibes-Regular.ttf");

                    if (System.IO.File.Exists(fontPath))
                    {
                        BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                        nameFont = new iTextSharp.text.Font(bf, 38);
                    }
                    else
                    {
                        nameFont = FontFactory.GetFont("Times-Roman", 30, iTextSharp.text.Font.ITALIC);
                    }

                    // ================= NAME FONT French Script =================
                    iTextSharp.text.Font NameFont;

                    try
                    {
                        string frenchScriptPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "FRSCRIPT.TTF");

                        if (System.IO.File.Exists(frenchScriptPath))
                        {
                            // Register the font first
                            FontFactory.Register(frenchScriptPath, "FrenchScript");

                            // Then get it from the factory with specific encoding
                            NameFont = FontFactory.GetFont("FrenchScript", BaseFont.IDENTITY_H, BaseFont.EMBEDDED, 38f, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                        }
                        else
                        {
                            // fallback
                            NameFont = FontFactory.GetFont("Times-Italic", 38, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error if needed
                        Console.WriteLine($"Font loading error: {ex.Message}");
                        NameFont = FontFactory.GetFont("Times-Italic", 38, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                    }

                    // ================= CENTURY FONT =================
                    iTextSharp.text.Font centuryFont;
                    string centuryPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "Century.ttf");

                    if (System.IO.File.Exists(centuryPath))
                    {
                        BaseFont bfCentury = BaseFont.CreateFont(centuryPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                        centuryFont = new iTextSharp.text.Font(bfCentury, 22, iTextSharp.text.Font.NORMAL); // size 22
                    }
                    else
                    {
                        // fallback
                        centuryFont = FontFactory.GetFont("Times-Roman", 22, iTextSharp.text.Font.NORMAL);
                    }
                   

                    // ================= TITLE (TOP LINE) =================
                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase("CERTIFICATE", titleFont),
                        centerX, pageSize.Height - 120, 0);

                    // ================= SUBTITLE =================
                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase("This accredits that", centuryFont),
                        centerX, pageSize.Height - 160, 0);


                    // ================= NAME =================
                    Chunk LnameChunk = new Chunk(certificate.RecipientName ?? "", NameFont);
                    Phrase namePhrase = new Phrase(LnameChunk);

                    ColumnText.ShowTextAligned(
                        canvas,
                        Element.ALIGN_CENTER,
                        namePhrase,
                        centerX,
                        pageSize.Height - 210,
                        0
                    );

                    // ================= DESCRIPTION =================
                    string training = (certificate.TrainingTitle ?? "Leadership/Governance").ToUpper();
                    string descPrefix = "has successfully completed the course";
                    string descSuffix = "as prescribed by Amtech Technologies Ltd";

                    // ================= LOCATION =================
                    string locationValue = certificate.Location ?? "Greenland Holiday Center - Molo";
                    string location = "Held at " + locationValue;
                    string locDatePrefix = " on ";

                    // Fonts
                    iTextSharp.text.Font mainFont = new iTextSharp.text.Font(centuryFont.BaseFont, 18, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);
                    iTextSharp.text.Font trainingFont = FontFactory.GetFont("Times-Roman", 26, iTextSharp.text.Font.BOLD, new BaseColor(128, 0, 0)); // maroon
                    iTextSharp.text.Font boldFont = new iTextSharp.text.Font(centuryFont.BaseFont, 18, iTextSharp.text.Font.BOLD, BaseColor.BLACK);


                    // -------- LINE 1 --------
                    Phrase line1 = new Phrase();
                    line1.Add(new Chunk(descPrefix, mainFont));

                    ColumnText ct1 = new ColumnText(canvas);
                    ct1.SetSimpleColumn(
                        line1,
                        50, pageSize.Height - 340,
                        pageSize.Width - 50, pageSize.Height - 250,
                        25, Element.ALIGN_CENTER
                    );
                    ct1.Go();


                    // -------- LINE 2 --------
                    Phrase line2 = new Phrase();
                    line2.Add(new Chunk(training, trainingFont));

                    ColumnText ct2 = new ColumnText(canvas);
                    ct2.SetSimpleColumn(
                        line2,
                        50, pageSize.Height - 380,
                        pageSize.Width - 50, pageSize.Height - 280,
                        25, Element.ALIGN_CENTER
                    );
                    ct2.Go();


                    // -------- LINE 3 (WITH "Held at") --------
                    Phrase line3 = new Phrase();
                    line3.Add(new Chunk(descSuffix + " ", mainFont));
                    line3.Add(new Chunk(location, boldFont)); // includes "Held at"
                    line3.Add(new Chunk(locDatePrefix, mainFont));
                    line3.Add(new Chunk(certificate.EventDate.ToString("dddd d MMMM yyyy"), boldFont));

                    ColumnText ct3 = new ColumnText(canvas);
                    ct3.SetSimpleColumn(
                        line3,
                        50, pageSize.Height - 420,
                        pageSize.Width - 50, pageSize.Height - 310,
                        25, Element.ALIGN_CENTER
                    );
                    ct3.Go();



                    // ================= QR CODE GENERATION =================
                    string qrText = "https://www.amtechafrica.com";

                    QRCodeGenerator qrGenerator = new QRCodeGenerator();
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrText, QRCodeGenerator.ECCLevel.Q);
                    QRCode qrCode = new QRCode(qrCodeData);

                    using (Bitmap qrBitmap = qrCode.GetGraphic(20))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        qrBitmap.Save(ms, ImageFormat.Png);
                        byte[] qrBytes = ms.ToArray();

                        iTextSharp.text.Image qrImage = iTextSharp.text.Image.GetInstance(qrBytes);

                        // ✅ Reduce size (cleaner look)
                        qrImage.ScaleAbsolute(70f, 70f);

                        // ✅ RIGHT SIDE positioning (safe from signatures)
                        float qrX = pageSize.Width - 120;   // right margin
                        float qrY = pageSize.Height - 420;  // just below line 3, above signatures

                        qrImage.SetAbsolutePosition(qrX, qrY);

                        // Add to PDF
                        canvas.AddImage(qrImage);

                        // ================= OPTIONAL LABEL =================
                        ColumnText.ShowTextAligned(canvas,
                            Element.ALIGN_CENTER,
                            new Phrase("Scan to verify", smallFont),
                            qrX + 35, qrY - 10, 0); // centered under QR
                    }


                    // LEFT SIDE: CEO Signature
                    if (certificate.CEOSignature != null)
                    {
                        using var ms = new MemoryStream(certificate.CEOSignature);
                        var ceoSig = iTextSharp.text.Image.GetInstance(ms.ToArray());

                        ceoSig.ScaleAbsolute(180f, 105f); // Increased from 100x50 to 150x75
                        ceoSig.SetAbsolutePosition(120f, 85f); // Slightly adjusted position
                        ceoSig.Alignment = iTextSharp.text.Image.ALIGN_LEFT;

                        canvas.AddImage(ceoSig);
                    }

                    // RIGHT SIDE: Trainer Signature
                    if (certificate.TrainerSignature != null)
                    {
                        using var ms = new MemoryStream(certificate.TrainerSignature);
                        var trainerSig = iTextSharp.text.Image.GetInstance(ms.ToArray());

                        trainerSig.ScaleAbsolute(180f, 105f); // Increased from 100x50 to 150x75
                        trainerSig.SetAbsolutePosition(pageSize.Width - 290f, 85f); // Adjusted position
                        trainerSig.Alignment = iTextSharp.text.Image.ALIGN_RIGHT;

                        canvas.AddImage(trainerSig);
                    }


                    // ================= SIGNATURE TEXT (ALIGNED BELOW LINES) =================
                    float leftLineCenterX = 150f;   // center of left line
                    float rightLineCenterX = pageSize.Width - 250f; // center of right line

                    // Move text BELOW the lines
                    float nameY = 85f;     // Names just below line
                    float titleY = 65f;    // Titles below names

                    // Fonts
                    var sigNameFont = FontFactory.GetFont("Times-Roman", 14, iTextSharp.text.Font.NORMAL);
                    var sigTitleFont = FontFactory.GetFont("Times-Roman", 12, iTextSharp.text.Font.BOLD);

                    // ================= LEFT SIDE (CEO) =================
                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase(certificate.CEO_Name ?? "CEO Name", sigNameFont),
                        leftLineCenterX, nameY, 0);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase(certificate.CEO_Title ?? "CEO Title", sigTitleFont),
                        leftLineCenterX, titleY, 0);

                    // ================= RIGHT SIDE (TRAINER) =================
                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase(certificate.Trainer_Name ?? "Trainer Name", sigNameFont),
                        rightLineCenterX, nameY, 0);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_CENTER,
                        new Phrase(certificate.Trainer_Title ?? "Trainer Title", sigTitleFont),
                        rightLineCenterX, titleY, 0);

                    // ================= FOOTER =================
                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_LEFT,
                        new Phrase($"Cert No: {certificate.CertificateNumber ?? "0000"}", smallFont),
                        50, 40, 0);

                    ColumnText.ShowTextAligned(canvas, Element.ALIGN_RIGHT,
                        new Phrase($"Issue Date: {certificate.IssueDate:yyyy-MM-dd}", smallFont),
                        pageSize.Width - 50, 40, 0);

                    document.Close();
                    writer.Close();

                    byte[] pdfBytes = stream.ToArray();
                    string fileName = $"Certificate_{certificate.RecipientName?.Replace(" ", "_")}.pdf";

                    return File(pdfBytes, "application/pdf", fileName);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error generating certificate: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Certificates/GenerateAndDownload/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAndDownload(int id)
        {
            return await Generate(id);
        }
    }
}