using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Reports.Data;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Drawing;
using PdfSharpCore.Drawing.Layout;
using PdfSharpCore.Pdf.IO;
using Reports.Data;
using Reports.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Path = System.IO.Path;

public class UploadedFilesController : Controller
{
    private readonly ReportsDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly string _uploadPath;

    public UploadedFilesController(ReportsDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
        _uploadPath = Path.Combine(env.WebRootPath, "uploads");
    }

    public async Task<IActionResult> Index()
    {
        var files = await _context.UploadedFiles
            .Include(f => f.PaymentVoucher)
            .ToListAsync();
        return View(files);
    }

    public async Task<IActionResult> Details(int id)
    {
        var file = await _context.UploadedFiles
            .Include(f => f.PaymentVoucher)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
            return NotFound();

        return View(file);
    }

    [HttpGet]
    public IActionResult Upload()
    {
        // fixed typo: "VoucherName"
        ViewBag.Vouchers = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName");

        var model = new FileUploadViewModel
        {
            PaymentVoucherId = 0,
            FileNames = new List<string>(),
            Files = new List<IFormFile>()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(FileUploadViewModel model)
    {
        // repopulate dropdown when re-displaying view
        ViewBag.Vouchers = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            EnsureUploadDirectoryExists();
            var batchId = Guid.NewGuid().ToString();

            // Save files (adds entities to _context)
            await ProcessUploadedFiles(model, batchId);

            // commit DB once after adding everything
            await _context.SaveChangesAsync();

            TempData["Message"] = "Files uploaded successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            var errorMessage = ex.InnerException != null
                ? $"{ex.Message} | Inner: {ex.InnerException.Message}"
                : ex.Message;

            ModelState.AddModelError(string.Empty, $"Error uploading files: {errorMessage}");
            return View(model);
        }
    }

    private async Task ProcessUploadedFiles(FileUploadViewModel model, string batchId)
    {
        if (model.Files == null || model.Files.Count == 0) return;

        for (int i = 0; i < model.Files.Count; i++)
        {
            var file = model.Files[i];
            if (file == null || file.Length == 0) continue;

            // Save to wwwroot/uploads using the controller's _uploadPath
            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var physicalPath = Path.Combine(_uploadPath, uniqueFileName);

            // write file to disk
            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // determine display name (if provided in FileNames list)
            string displayName = null;
            if (model.FileNames != null && i < model.FileNames.Count && !string.IsNullOrWhiteSpace(model.FileNames[i]))
            {
                displayName = model.FileNames[i].Trim();
            }
            else
            {
                displayName = Path.GetFileNameWithoutExtension(file.FileName);
            }

            // Build uploaded file entity (ensure property names match your model)
            var uploaded = new UploadedFile
            {
                PaymentVoucherId = model.PaymentVoucherId,
                FileName = displayName,
                FilePath = $"/uploads/{uniqueFileName}",     // web-accessible relative path
                FileType = file.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true ? "Image" : "Document",
                UploadBatchId = batchId,                    // IMPORTANT: use UploadBatchId (used elsewhere in your code)
                UploadedOn = DateTime.UtcNow
            };

            _context.UploadedFiles.Add(uploaded);
        }
    }

    public IActionResult ViewBatch(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
            return BadRequest();

        var files = _context.UploadedFiles
            .Where(f => f.UploadBatchId == batchId)
            .Include(f => f.PaymentVoucher)
            .ToList();

        if (!files.Any())
            return NotFound();

        return View(files);
    }
    


    [HttpGet("UploadedFiles/Edit/{batchId}")]
    public async Task<IActionResult> Edit(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
            return BadRequest();

        var files = await _context.UploadedFiles
            .Where(f => f.UploadBatchId == batchId)
            .OrderBy(f => f.UploadedOn)
            .Include(f => f.PaymentVoucher)
            .ToListAsync();

        if (!files.Any())
            return NotFound();

        var model = new FileUploadViewModel
        {
            PaymentVoucherId = files.First().PaymentVoucherId,
            FileNames = files.Select(f => f.FileName).ToList(),
            ExistingFiles = files
        };

        ViewBag.BatchId = batchId;
        ViewBag.Vouchers = new SelectList(_context.PaymentVouchers, "PaymentVoucherId", "VoucheName", model.PaymentVoucherId);

        return View(model);
    }

    [HttpPost("UploadedFiles/Edit/{batchId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string batchId, int[]? existingIds, string[]? existingNames,
    IFormFile[]? replaceFiles, IFormFile[]? newFiles, string[]? newFileNames, int? paymentVoucherId
    )
    {
        if (string.IsNullOrEmpty(batchId))
            return BadRequest();

        var existingFiles = await _context.UploadedFiles
            .Where(f => f.UploadBatchId == batchId)
            .ToListAsync();

        if (!existingFiles.Any())
            return NotFound();

        try
        {
            // ✅ Update file names
            if (existingIds != null && existingNames != null)
            {
                for (int i = 0; i < existingIds.Length && i < existingNames.Length; i++)
                {
                    var file = existingFiles.FirstOrDefault(f => f.Id == existingIds[i]);
                    if (file != null && !string.IsNullOrWhiteSpace(existingNames[i]))
                    {
                        file.FileName = existingNames[i].Trim();
                    }
                }
            }

            // ✅ Replace existing files with uploaded ones
            if (replaceFiles != null && existingIds != null)
            {
                for (int i = 0; i < replaceFiles.Length && i < existingIds.Length; i++)
                {
                    var replacementFile = replaceFiles[i];
                    if (replacementFile == null || replacementFile.Length == 0) continue;

                    var file = existingFiles.FirstOrDefault(f => f.Id == existingIds[i]);
                    if (file == null) continue;

                    // Delete old file from disk
                    DeletePhysicalFile(file.FilePath!);

                    // Save new file
                    var newFileName = await SaveFileToDisk(replacementFile);

                    // Update entity
                    file.FilePath = $"/uploads/{newFileName}";
                    file.FileType = replacementFile.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
                        ? "Image" : "Document";
                    file.UploadedOn = DateTime.UtcNow;
                }
            }

            // ✅ Add new files
            if (newFiles != null)
            {
                for (int i = 0; i < newFiles.Length; i++)
                {
                    var newFile = newFiles[i];
                    if (newFile == null || newFile.Length == 0) continue;

                    var savedFileName = await SaveFileToDisk(newFile);
                    var customName = (newFileNames != null && i < newFileNames.Length)
                        ? newFileNames[i] : null;

                    _context.UploadedFiles.Add(new UploadedFile
                    {
                        PaymentVoucherId = paymentVoucherId ?? existingFiles.First().PaymentVoucherId,
                        FileName = !string.IsNullOrWhiteSpace(customName)
                            ? customName.Trim()
                            : Path.GetFileNameWithoutExtension(newFile.FileName),
                        FilePath = $"/uploads/{savedFileName}",
                        FileType = newFile.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
                            ? "Image" : "Document",
                        UploadBatchId = batchId,
                        UploadedOn = DateTime.UtcNow
                    });
                }
            }

            // ✅ Update PaymentVoucherId for all files in this batch (if changed)
            if (paymentVoucherId.HasValue)
            {
                foreach (var file in existingFiles)
                {
                    file.PaymentVoucherId = paymentVoucherId.Value;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = "Changes saved successfully!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error saving changes: {ex.Message}";
            return RedirectToAction(nameof(Edit), new { batchId });
        }
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFile(int id)
    {
        var file = await _context.UploadedFiles.FindAsync(id);
        if (file == null)
        {
            TempData["Error"] = "File not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            DeletePhysicalFile(file.FilePath!);
            _context.UploadedFiles.Remove(file);
            await _context.SaveChangesAsync();

            TempData["Message"] = "File deleted successfully.";
            return RedirectToAction(nameof(Edit), new { batchId = file.UploadBatchId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error deleting file: {ex.Message}";
            return RedirectToAction(nameof(Edit), new { batchId = file.UploadBatchId });
        }
    }

    public async Task<IActionResult> Delete(int id)
    {
        var file = await _context.UploadedFiles.FindAsync(id);
        if (file == null) return NotFound();
        return View(file);
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var file = await _context.UploadedFiles.FindAsync(id);
        if (file == null)
            return RedirectToAction(nameof(Index));

        try
        {
            DeletePhysicalFile(file.FilePath!);
            _context.UploadedFiles.Remove(file);
            await _context.SaveChangesAsync();

            TempData["Message"] = "File deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error deleting file: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete(int[] selectedIds)
    {
        if (selectedIds == null || selectedIds.Length == 0)
        {
            TempData["Error"] = "No files selected for deletion.";
            return RedirectToAction(nameof(Index));
        }

        var files = await _context.UploadedFiles
            .Where(f => selectedIds.Contains(f.Id))
            .ToListAsync();

        if (!files.Any())
        {
            TempData["Error"] = "No valid files found for deletion.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            foreach (var file in files)
            {
                DeletePhysicalFile(file.FilePath!);
                _context.UploadedFiles.Remove(file);
            }

            await _context.SaveChangesAsync();
            TempData["Message"] = $"{files.Count} file(s) deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error deleting files: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    #region Private Helper Methods

    private void EnsureUploadDirectoryExists()
    {
        if (!Directory.Exists(_uploadPath))
            Directory.CreateDirectory(_uploadPath);
    }

    private async Task<string> SaveFileToDisk(IFormFile file)
    {
        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(_uploadPath, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return uniqueFileName;
    }

    // ✅ Added PaymentVoucherId
    private UploadedFile CreateUploadedFileEntity(FileUploadViewModel model, int index,
        IFormFile file, string fileName, string batchId)
    {
        return new UploadedFile
        {
           // PaymentVoucherId = paymentVoucherId,
            FileName = GetFileName(model, index, file),
            FilePath = $"/uploads/{fileName}",
            FileType = file.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase)
                ? "Image" : "Document",
            UploadBatchId = batchId
        };
    }

    private string GetFileName(FileUploadViewModel model, int index, IFormFile file)
    {
        return (model.FileNames != null && index < model.FileNames.Count &&
                !string.IsNullOrWhiteSpace(model.FileNames[index]))
            ? model.FileNames[index].Trim()
            : Path.GetFileNameWithoutExtension(file.FileName);
    }

    private async Task<List<UploadedFile>> GetFilesByBatchId(string batchId)
    {
        return await _context.UploadedFiles
            .Where(f => f.UploadBatchId == batchId)
            .OrderBy(f => f.UploadedOn)
            .ToListAsync();
    }

    private void UpdateExistingFileNames(int[]? existingIds, string[]? existingNames,
        List<UploadedFile> existingFiles)
    {
        if (existingIds == null || existingNames == null) return;

        for (int i = 0; i < existingIds.Length && i < existingNames.Length; i++)
        {
            var file =  existingFiles.FirstOrDefault(f => f.Id == existingIds[i]);
            if (file != null && !string.IsNullOrWhiteSpace(existingNames[i]))
            {
                file.FileName = existingNames[i].Trim();
            }
        }
    }

    private async Task ReplaceExistingFiles(IFormFile[]? replaceFiles, int[]? existingIds,
        List<UploadedFile> existingFiles)
    {
        if (replaceFiles == null || existingIds == null) return;

        for (int i = 0; i < replaceFiles.Length && i < existingIds.Length; i++)
        {
            var replacementFile = replaceFiles[i];
            if (replacementFile == null || replacementFile.Length == 0) continue;

            var file = existingFiles.FirstOrDefault(f => f.Id == existingIds[i]);
            if (file == null) continue;

            // Delete old file
            DeletePhysicalFile(file.FilePath!);

            // Save new file
            var newFileName = await SaveFileToDisk(replacementFile);

            // Update entity
            file.FilePath = $"/uploads/{newFileName}";
            file.FileType = replacementFile.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
                ? "Image" : "Document";
            file.UploadedOn = DateTime.UtcNow;
        }
    }

    // ✅ Added PaymentVoucherId param
    private async Task AddNewFiles(IFormFile[]? newFiles, string[]? newFileNames, string batchId, int paymentVoucherId)
    {
        if (newFiles == null) return;

        for (int i = 0; i < newFiles.Length; i++)
        {
            var newFile = newFiles[i];
            if (newFile == null || newFile.Length == 0) continue;

            var fileName = await SaveFileToDisk(newFile);
            var customName = (newFileNames != null && i < newFileNames.Length)
                ? newFileNames[i] : null;

            _context.UploadedFiles.Add(new UploadedFile
            {
                PaymentVoucherId = paymentVoucherId,
                FileName = !string.IsNullOrWhiteSpace(customName)
                    ? customName.Trim()
                    : Path.GetFileNameWithoutExtension(newFile.FileName),
                FilePath = $"/uploads/{fileName}",
                FileType = newFile.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
                    ? "Image" : "Document",
                UploadBatchId = batchId,
                UploadedOn = DateTime.UtcNow
            });
        }
    }

    private void DeletePhysicalFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var physicalPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
        if (System.IO.File.Exists(physicalPath))
        {
            System.IO.File.Delete(physicalPath);
        }
    }
    #endregion











    [HttpGet]
    public async Task<IActionResult> DownloadFullBatchPdf(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
            return BadRequest("BatchId is required.");

        var files = await _context.UploadedFiles
            .Where(f => f.UploadBatchId == batchId)
            .OrderBy(f => f.UploadedOn)
            .ToListAsync();

        if (!files.Any())
            return NotFound("No files found for this batch.");

        using var outputDoc = new PdfSharpCore.Pdf.PdfDocument();

        // -------- 1. Add all images first --------
        var imageFiles = files.Where(f => f.FileType == "Image").ToList();
        foreach (var file in imageFiles)
        {
            var physicalPath = Path.Combine(_env.WebRootPath, file.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(physicalPath)) continue;

            var page = outputDoc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;

            using var gfx = XGraphics.FromPdfPage(page);
            using var xImage = XImage.FromFile(physicalPath);

            double margin = 20;
            double maxW = page.Width - margin * 2;
            double maxH = page.Height - margin * 2;
            double scale = Math.Min(maxW / xImage.PixelWidth, maxH / xImage.PixelHeight);
            if (scale > 1) scale = 1;

            double drawW = xImage.PixelWidth * scale;
            double drawH = xImage.PixelHeight * scale;
            double x = (page.Width.Point - drawW) / 2;
            double y = (page.Height.Point - drawH) / 2;

            gfx.DrawImage(xImage, x, y, drawW, drawH);
        }

        // -------- 2. Add documents (PDF, DOCX, TXT) --------
        var docFiles = files.Where(f => f.FileType == "Document").ToList();
        foreach (var file in docFiles)
        {
            var physicalPath = Path.Combine(_env.WebRootPath, file.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(physicalPath)) continue;

            var ext = Path.GetExtension(physicalPath).ToLowerInvariant();

            try
            {
                if (ext == ".pdf")
                {
                    using var inputPdf = PdfSharpCore.Pdf.IO.PdfReader.Open(physicalPath, PdfDocumentOpenMode.Import);
                    foreach (var page in inputPdf.Pages)
                        outputDoc.AddPage(page);
                }
                else if (ext == ".docx")
                {
                    string text = ExtractTextFromDocx(physicalPath);
                    if (!string.IsNullOrWhiteSpace(text))
                        AddTextAsPdfPages(text, outputDoc);
                }
                else if (ext == ".txt")
                {
                    string text = await System.IO.File.ReadAllTextAsync(physicalPath);
                    if (!string.IsNullOrWhiteSpace(text))
                        AddTextAsPdfPages(text, outputDoc);
                }
            }
            catch
            {
                continue; // skip unsupported or corrupted files
            }
        }

        if (outputDoc.PageCount == 0)
            return BadRequest("No valid pages could be generated from this batch.");

        using var ms = new MemoryStream();
        outputDoc.Save(ms, false);

        return File(ms.ToArray(), "application/pdf", $"Batch_{batchId}_Combined.pdf");
    }

    // ----------------- Helpers -----------------

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

    private void AddTextAsPdfPages(string text, PdfSharpCore.Pdf.PdfDocument outputDoc)
    {
        const int approxCharsPerPage = 3000; // split text into pages
        var chunks = ChunkText(text, approxCharsPerPage);
        var font = new XFont("Arial", 12);

        foreach (var chunk in chunks)
        {
            var page = outputDoc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            var tf = new XTextFormatter(gfx);
            var rect = new XRect(40, 40, page.Width - 80, page.Height - 80);
            tf.DrawString(chunk, font, XBrushes.Black, rect/*, XStringFormats.Default*/); // ✔ corrected
        }
    }

    private static IEnumerable<string> ChunkText(string text, int chunkSize)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        int index = 0;
        while (index < text.Length)
        {
            int length = System.Math.Min(chunkSize, text.Length - index);
            yield return text.Substring(index, length);
            index += length;
        }
    }

}
