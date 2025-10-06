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
    public class PaymentVouchersController : Controller
    {
        private readonly ReportsDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly string _uploadPath;

        public PdfImageXObject imgData { get; private set; }
        public string VoucherNumber { get; private set; }

        public PaymentVouchersController(ReportsDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
            //_uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            //EnsureUploadDirectoryExists();

            _uploadPath = Path.Combine(env.WebRootPath, "uploads");  // physical path under wwwroot
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }

        }

        // GET: PaymentVouchers
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var vouchers = await _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.Summaries)
                .Include(v => v.UploadedFile)
                .ToListAsync();
            return View(vouchers);
        }

        //[HttpGet]
        //public async Task<IActionResult> ViewBatch(int? paymentVoucherId)
        //{
        //    if (paymentVoucherId == null)
        //    {
        //        return BadRequest("Invalid payment voucher ID.");
        //    }

        //    var files = await _context.UploadedFiles
        //        .Where(f => f.PaymentVoucherId == paymentVoucherId)
        //        .OrderBy(f => f.FileName)
        //        .ToListAsync();

        //    if (files == null || !files.Any())
        //    {
        //        TempData["ErrorMessage"] = "No files found for this voucher.";
        //        return RedirectToAction("Index", "PaymentVoucher"); // 👈 redirect back safely
        //    }

        //    return View(files);
       // }


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

        // GET: Create Voucher + Files
        [HttpGet]
        public IActionResult CreateWithFiles()
        {
            var model = new PaymentVoucherWithFilesViewModel
            {
                PaymentVoucher = new PaymentVoucher
                {
                    Date = DateTime.Now,
                    //VoucherNumber = "B2C-" + Guid.NewGuid().ToString().Substring(0, 8), // unique number
                    VoucheName = VoucherNumber,  // 👈 set VoucherName = VoucherNumber
                    //VoucherNumber = $"{DateTime.Now:yyyy/MM/dd}",
                    ChequeNo = "B2C"
                },
                Items = new List<PaymentVoucherItem>(),
                Files = new List<IFormFile>(),
                FileNames = new List<string>(),
                InfavourOf = new List<string>(),
                ExistingFiles = new List<UploadedFile>()
            };

            // Load descriptions for dropdown
            ViewBag.VoucherDescriptions = _context.VoucherDescriptions
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToList();
            return View(model);
        }


        // POST: Save Voucher + Items + Files
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWithFiles(PaymentVoucherWithFilesViewModel model)
        {
            var voucher = model.PaymentVoucher;
            voucher.Date = DateTime.Now;
            //voucher.VoucherNumber = "B2C-" + Guid.NewGuid().ToString().Substring(0, 8); // unique number
            voucher.VoucheName = voucher.VoucherNumber;  // 👈 keep them the same
            voucher.ChequeNo = "B2C";

            // Ensure items list is initialized
            if (model.Items == null || !model.Items.Any())
                model.Items = new List<PaymentVoucherItem>();

            // Process voucher items
            foreach (var item in model.Items.Where(i => !string.IsNullOrWhiteSpace(i.Description)))
            {
                // Ensure description exists in DB
                if (!_context.VoucherDescriptions.Any(d => d.Name == item.Description))
                {
                    _context.VoucherDescriptions.Add(new VoucherDescription { Name = item.Description });
                }

                // Calculate item amount
                item.Amount = (item.EachAmount + item.MpesaCharges) * item.ItemNo;
                item.PaymentVoucher = voucher;
            }

            // Set totals
            voucher.Items = model.Items;
            voucher.TotalAmount = voucher.Items.Sum(i => i.Amount) - voucher.VAT;
            voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

            //voucher.PreparedBy = User.Identity?.Name;
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (string.IsNullOrEmpty(userId)) return Unauthorized();
            //voucher.CreatedById = int.Parse(userId);

            // Save voucher + items + new descriptions
            _context.PaymentVouchers.Add(voucher);
            await _context.SaveChangesAsync();

            //Handle file uploads
            if (model.Files != null && model.Files.Any())
            {
                EnsureUploadDirectoryExists();
                var batchId = Guid.NewGuid().ToString();
                int i = 0;

                foreach (var file in model.Files)
                {
                    if (file != null && file.Length > 0)
                    {
                        var fileName = $"{batchId}_{Path.GetFileName(file.FileName)}";
                        var filePath = Path.Combine(_env.WebRootPath, "uploads", fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var uploaded = new UploadedFile
                        {
                            PaymentVoucherId = voucher.PaymentVoucherId,
                            FileName = model.FileNames?.ElementAtOrDefault(i) ?? file.FileName,
                            InFavourOf = model.InfavourOf?.ElementAtOrDefault(i) ?? "",
                            FilePath = "/uploads/" + fileName,
                            FileType = file.ContentType ?? Path.GetExtension(file.FileName),
                            UploadBatchId = batchId
                        };

                        _context.UploadedFiles.Add(uploaded);
                        i++;
                    }
                }

                await _context.SaveChangesAsync();
            }

            TempData["Message"] = "Voucher, items, descriptions and files saved successfully!";
            return RedirectToAction(nameof(Index));
        }



        //Get: Edit with files

        [HttpGet]
        public async Task<IActionResult> EditWithFiles(int id)
        {
            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.UploadedFile)
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher == null)
                return NotFound();

            var vm = new PaymentVoucherEditViewModel
            {
                PaymentVoucher = voucher,
                Items = voucher.Items.ToList(),
                ExistingFiles = voucher.UploadedFile.ToList()
            };

            ViewBag.VoucherDescriptions = await _context.VoucherDescriptions
                .Select(d => d.Name)
                .ToListAsync();

            return View(vm);
        }


        //Post:Post edit with files
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditWithFiles(
        PaymentVoucherEditViewModel model,
        string[]? itemDescriptions,
        IFormFile[]? replaceFiles)
        {
            ViewBag.VoucherDescriptions = await _context.VoucherDescriptions.Select(d => d.Name).ToListAsync();

            if (model == null || model.PaymentVoucher == null)
            {
                ModelState.AddModelError("", "No voucher data submitted.");
                TempData["Error"] = "No voucher data submitted.";
                return View(model);
            }

            try
            {
                var voucher = model.PaymentVoucher;

                // Ensure VoucherNumber exists (Required)
                if (string.IsNullOrWhiteSpace(voucher.VoucherNumber))
                {
                    voucher.VoucherNumber = voucher.VoucheName;
                }

                // Keep VoucherName = VoucherNumber
                voucher.VoucheName = voucher.VoucherNumber;

                // Always refresh system fields
                voucher.Date = DateTime.Now;
                voucher.ChequeNo ??= "B2C";

                // --- Handle Descriptions ---
                if (itemDescriptions != null)
                {
                    foreach (var desc in itemDescriptions.Where(d => !string.IsNullOrWhiteSpace(d)))
                    {
                        var trimmed = desc.Trim();
                        if (!_context.VoucherDescriptions.Any(vd => vd.Name == trimmed))
                        {
                            _context.VoucherDescriptions.Add(new VoucherDescription { Name = trimmed });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                // --- Update Items ---
                var existingItems = _context.PaymentVoucherItems
                    .Where(i => i.PaymentVoucherId == voucher.PaymentVoucherId);
                _context.PaymentVoucherItems.RemoveRange(existingItems);

                if (model.Items != null && model.Items.Any())
                {
                    foreach (var item in model.Items)
                    {
                        item.PaymentVoucherId = voucher.PaymentVoucherId;

                        // 🚨 Fix: set navigation property so validation passes
                        item.PaymentVoucher = voucher;

                        // Formula
                        item.Amount = (item.EachAmount + item.MpesaCharges) * item.ItemNo;

                        _context.PaymentVoucherItems.Add(item);
                    }
                }

                voucher.Items = model.Items;

                // ✅ Calculate total minus VAT only once
                var totalBeforeVAT = model.Items?.Sum(i => i.Amount) ?? 0;
                var vatAmount = voucher.VAT ?? 0;
                voucher.TotalAmount = totalBeforeVAT - vatAmount;

                // Amount in Words
                voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

                _context.Update(voucher);

                // --- Update Existing Files ---
                if (model.ExistingIds != null)
                {
                    for (int i = 0; i < model.ExistingIds.Count; i++)
                    {
                        var file = await _context.UploadedFiles.FindAsync(model.ExistingIds[i]);
                        if (file != null)
                        {
                            file.FileName = model.ExistingNames?[i] ?? file.FileName;
                            file.InFavourOf = model.ExistingInfavourOf?[i] ?? file.InFavourOf;
                            _context.Update(file);
                        }
                    }
                }

                // --- Replace files (optional) ---
                if (replaceFiles != null && model.ExistingIds != null)
                {
                    for (int i = 0; i < model.ExistingIds.Count; i++)
                    {
                        var replacement = replaceFiles.ElementAtOrDefault(i);
                        if (replacement == null) continue;

                        var file = await _context.UploadedFiles.FindAsync(model.ExistingIds[i]);
                        if (file == null) continue;

                        var oldPath = Path.Combine("wwwroot", file.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);

                        var uploadsFolder = Path.Combine("wwwroot", "uploads");
                        Directory.CreateDirectory(uploadsFolder);
                        var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(replacement.FileName)}";
                        var newPath = Path.Combine(uploadsFolder, newFileName);

                        using (var stream = new FileStream(newPath, FileMode.Create))
                            await replacement.CopyToAsync(stream);

                        file.FilePath = "/uploads/" + newFileName;
                        file.FileType = replacement.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true
                            ? "Image" : "Document";
                        file.UploadedOn = DateTime.UtcNow;
                    }
                }


                // --- Delete existing files (if any were marked for removal) ---
                if (model.FilesToDelete != null && model.FilesToDelete.Any())
                {
                    var files = _context.UploadedFiles
                        .Where(f => model.FilesToDelete.Contains(f.Id))
                        .ToList();

                    _context.UploadedFiles.RemoveRange(files);
                }

                // --- Add new files ---
                if (model.NewFiles != null && model.NewFiles.Any())
                {
                    var batchId = (await _context.UploadedFiles
                        .Where(f => f.PaymentVoucherId == voucher.PaymentVoucherId)
                        .Select(f => f.UploadBatchId)
                        .FirstOrDefaultAsync()) ?? Guid.NewGuid().ToString();

                    for (int i = 0; i < model.NewFiles.Count; i++)
                    {
                        var file = model.NewFiles[i];
                        if (file == null || file.Length == 0) continue;

                        var uploadsFolder = Path.Combine("wwwroot", "uploads");
                        Directory.CreateDirectory(uploadsFolder);
                        var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                        var newPath = Path.Combine(uploadsFolder, newFileName);

                        using (var stream = new FileStream(newPath, FileMode.Create))
                            await file.CopyToAsync(stream);

                        _context.UploadedFiles.Add(new UploadedFile
                        {
                            PaymentVoucherId = voucher.PaymentVoucherId,
                            FileName = model.NewFileNames?[i] ?? Path.GetFileNameWithoutExtension(file.FileName),
                            InFavourOf = model.NewInFavourOf?[i] ?? string.Empty,
                            FilePath = "/uploads/" + newFileName,
                            FileType = file.ContentType?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true ? "Image" : "Document",
                            UploadBatchId = batchId,
                            UploadedOn = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Message"] = "Voucher updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error saving voucher: " + ex.GetBaseException().Message);
                TempData["Error"] = "Error saving voucher: " + ex.GetBaseException().Message;

                // reload files for redisplay
                model.ExistingFiles = await _context.UploadedFiles
                    .Where(f => f.PaymentVoucherId == model.PaymentVoucher.PaymentVoucherId)
                    .ToListAsync();

                return View(model);
            }
        }
        

        //helpers
        private void EnsureUploadDirectoryExists()
        {
            var uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }
        }

        //Post: saving new description
        [HttpPost]
        public async Task<IActionResult> AddDescription([FromBody] string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return BadRequest("Description cannot be empty.");

            // Only save if it doesn't exist
            if (!_context.VoucherDescriptions.Any(d => d.Name == description))
            {
                var newDesc = new VoucherDescription { Name = description };
                _context.VoucherDescriptions.Add(newDesc);
                await _context.SaveChangesAsync();
            }

            // Return all descriptions for refreshing dropdown/autocomplete
            var descriptions = await _context.VoucherDescriptions
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToListAsync();

            return Json(descriptions);
        }


        [HttpGet]
        public async Task<IActionResult> GetVoucherByNumber(string voucherNumber)
        {
            if (string.IsNullOrWhiteSpace(voucherNumber))
                return BadRequest("Voucher number required.");

            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.UploadedFile) // if you have navigation for files
                .FirstOrDefaultAsync(v => v.VoucherNumber == voucherNumber);

            if (voucher == null)
                return NotFound();

            return Json(new
            {
                voucher.PaymentVoucherId,
                voucher.VoucherNumber,
                voucher.Date,
                voucher.ChequeNo,
                voucher.TotalAmount,
                voucher.AmountInWords,
                voucher.PreparedBy,
                Items = voucher.Items.Select(i => new
                {
                    i.Id,
                    i.ItemNo,
                    i.Description,
                    i.EachAmount,
                    i.MpesaCharges,
                    i.Amount
                }).ToList(),
                Files = voucher.UploadedFile.Select(f => new
                {
                    f.FileName,
                    f.FilePath,
                    f.InFavourOf,
                    f.FileType
                }).ToList()
            });
        }

        //Get: create
        public IActionResult Create()
        {
            ViewBag.VoucherDescriptions = _context.VoucherDescriptions
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToList();

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
        public async Task<IActionResult> Create(PaymentVoucher voucher, string[] itemDescriptions)
        {
            voucher.Date = DateTime.Now;
            voucher.VoucherNumber = $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}";
            voucher.ChequeNo = "B2C";

            if (voucher.Items == null)
                voucher.Items = new List<PaymentVoucherItem>();

            // Handle saving new descriptions into DB
            foreach (var desc in itemDescriptions.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                if (!_context.VoucherDescriptions.Any(v => v.Name == desc))
                {
                    _context.VoucherDescriptions.Add(new VoucherDescription { Name = desc });
                }
            }

            // Calculate item amounts
            foreach (var item in voucher.Items)
            {
                item.Amount = (item.EachAmount + item.MpesaCharges) * item.ItemNo;
            }

            voucher.TotalAmount = voucher.Items.Sum(i => i.Amount);
            voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

            var username = User.Identity?.Name;
            voucher.PreparedBy = username;

            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (string.IsNullOrEmpty(userId)) return Unauthorized();
            //voucher.CreatedById = int.Parse(userId);

            _context.PaymentVouchers.Add(voucher);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        //Get: PaymentVouchers/Edit
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

            // Populate descriptions for the dropdown (same as Create GET)
            ViewBag.VoucherDescriptions = await _context.VoucherDescriptions
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToListAsync();

            return View(voucher);
        }

        // POST: PaymentVouchers/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PaymentVoucher voucher, string[] itemDescriptions)
        {
            if (id != voucher.PaymentVoucherId)
                return BadRequest("Voucher ID mismatch between route and model.");

            // Ensure Items collection
            voucher.Items ??= new List<PaymentVoucherItem>();

            // Preserve/regenerate system fields as Create does
            if (voucher.Date == default)
                voucher.Date = DateTime.Now;

            voucher.VoucherNumber ??= $"{DateTime.Now.Year}/{DateTime.Now.Month}/{DateTime.Now.Day}";
            voucher.ChequeNo ??= "B2C";

            // Save any new descriptions that came from the client
            if (itemDescriptions != null && itemDescriptions.Length > 0)
            {
                foreach (var desc in itemDescriptions.Where(d => !string.IsNullOrWhiteSpace(d)))
                {
                    if (!_context.VoucherDescriptions.Any(vd => vd.Name == desc))
                    {
                        _context.VoucherDescriptions.Add(new VoucherDescription { Name = desc.Trim() });
                    }
                }
                // Persist description additions immediately so subsequent rebuilds/readers will see them
                await _context.SaveChangesAsync();
            }

            // Recalculate each item's Amount using your formula
            foreach (var item in voucher.Items)
            {
                // ensure correct relation
                item.PaymentVoucherId = voucher.PaymentVoucherId;
                item.PaymentVoucher = voucher;

                // Apply formula (EachAmount + MpesaCharges) * ItemNo
                item.Amount = (item.EachAmount + item.MpesaCharges) * item.ItemNo;
            }

            // Totals & words
            voucher.TotalAmount = voucher.Items.Sum(i => i.Amount);
            voucher.AmountInWords = NumberToWordsConverter.ConvertAmountToWords(voucher.TotalAmount ?? 0);

            // Set PreparedBy to logged-in user (same as Create)
            //var username = User.Identity?.Name;
            //voucher.PreparedBy = username;

            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (string.IsNullOrEmpty(userId)) return Unauthorized();
            //voucher.CreatedById = int.Parse(userId);

            // Validate model after making server-side adjustments
            ModelState.Clear();
            TryValidateModel(voucher);

            if (!ModelState.IsValid)
            {
                // Repopulate descriptions for the view and return
                ViewBag.VoucherDescriptions = await _context.VoucherDescriptions
                    .OrderBy(d => d.Name)
                    .Select(d => d.Name)
                    .ToListAsync();

                return View(voucher);
            }

            try
            {
                // Update voucher itself
                _context.Update(voucher);

                // Sync items: remove deleted, update existing, add new
                var existingItems = _context.PaymentVoucherItems
                    .Where(i => i.PaymentVoucherId == voucher.PaymentVoucherId)
                    .ToList();

                // Remove items that are not present in voucher.Items
                foreach (var existing in existingItems)
                {
                    if (!voucher.Items.Any(i => i.Id == existing.Id))
                        _context.PaymentVoucherItems.Remove(existing);
                }

                // Add or update items
                foreach (var item in voucher.Items)
                {
                    if (item.Id == 0) // new item
                        _context.PaymentVoucherItems.Add(item);
                    else // existing item -> ensure tracked update
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
            return _context.PaymentVouchers.Any(e => e.PaymentVoucherId == id);
        }

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
                .Include(v => v.Summaries) // ✅ include summaries to check dependency
                .FirstOrDefaultAsync(v => v.PaymentVoucherId == id);

            if (voucher == null)
            {
                return NotFound();
            }

            // ✅ Check if this voucher has summaries linked
            if (voucher.Summaries != null && voucher.Summaries.Any())
            {
                TempData["ErrorMessage"] = "❌ You cannot delete this voucher because it has linked summaries.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ Safe to delete
            if (voucher.Items != null && voucher.Items.Any())
            {
                _context.PaymentVoucherItems.RemoveRange(voucher.Items);
            }

            _context.PaymentVouchers.Remove(voucher);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Voucher deleted successfully.";
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

        //Access reports outsiide index
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Reports(string voucherName = null)
        {
            IQueryable<PaymentVoucher> query = _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.UploadedFile)
                .Include(v => v.Summaries)
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

        //Generate Voucher PDF
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
                var document = new Document(pdf, PageSize.A4);

                PdfFont times = PdfFontFactory.CreateFont(StandardFonts.TIMES_ROMAN);
                document.SetFont(times);

                var stampFrame = new Div()
                    .SetBorder(new DashedBorder(new DeviceRgb(173, 216, 230), 0.5f))
                    .SetPadding(3)
                    .SetWidth(150)
                    .SetHeight(100)
                    .SetTextAlignment(TextAlignment.CENTER);

                var stampText = new Paragraph()
                    .Add(new Text("AMTECH TECHNOLOGIES LIMITED\n")
                        .SetFontSize(8).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)))
                    .Add(new Text("P.O. BOX 79701-00200, NAIROBI\n")
                        .SetFontSize(8).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)))
                    //.Add(new Text("DATE: ").SetFontSize(10).SetFontColor(new DeviceRgb(173, 216, 230)))
                    //.Add(new Text($"{voucher.Date:dd/MM/yyyy}\n").SetFontSize(10).SetFontColor(new DeviceRgb(173, 216, 230)).SetUnderline())
                    .Add(new Text("PAID")
                        .SetFontSize(20).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)));

                document.ShowTextAligned(
                    stampText,
                    400, 400, // position center
                    pdf.GetNumberOfPages(),
                    TextAlignment.CENTER,
                    VerticalAlignment.MIDDLE,
                    (float)(Math.PI / 6) // 👈 rotate 30°
                );

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
                var title = new Paragraph("Cheque/M-pesa/Cash Payment Voucher")
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontSize(18)
                    .SetBold()
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
                    //table.AddCell(i.ToString());
                    table.AddCell(item.ItemNo.ToString());
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

                // --- Add Failed Section only if data exists ---
                if (!string.IsNullOrWhiteSpace(voucher.FailedDesc) || (voucher.VAT.HasValue && voucher.VAT.Value > 0))
                {
                    Paragraph failedLine = new Paragraph()
                        .SetFontSize(11)
                        .Add(new Text(" "))
                        .Add(new Text(voucher.FailedDesc ?? "N/A")/*.SetUnderline(0.5f, -1)*/)
                        .Add(new Text("  ")) // small space
                        .Add(new Text(" and Total failed Amount: "))
                        .Add(new Text((voucher.VAT ?? 0).ToString("N2"))/*.SetUnderline(0.5f, -1)*/);

                    document.Add(failedLine);
                }

                // --- Amount in words ---
                Paragraph wordsLine = new Paragraph()
                    .SetFontSize(11)
                    .Add(new Text("Amount in words: "))
                    .Add(new Text(voucher.AmountInWords ?? "").SetUnderline(0.5f, -1));
                document.Add(wordsLine);

                // --- Payment Made in ---
                string paymentText = (!string.IsNullOrEmpty(voucher.PaymentMode) &&
                    voucher.PaymentMode.Equals("M-pesa", StringComparison.OrdinalIgnoreCase))
                    ? "Ksh B2C"
                    : "Ksh (Cash)";

                Paragraph paymentLine = new Paragraph()
                    .SetFontSize(11)
                    .Add(new Text("Payment Made in: "))
                    .Add(new Text(paymentText).SetUnderline(0.5f, -1));
                document.Add(paymentLine);



                // signatures with images
                // === Prepared By ===
                var preparedByPara = new Paragraph("\nPrepared By: ")
                    .Add(new Text(voucher.PreparedBy ?? "").SetUnderline())
                    .Add("   Sign: ");

                // Pick the correct image from wwwroot/signatures
                string preparedBySignaturePath = Path.Combine("wwwroot", "signatures", "prepared_by.png");
                if (System.IO.File.Exists(preparedBySignaturePath))
                {
                    var preparedSig = new Image(ImageDataFactory.Create(preparedBySignaturePath))
                        .ScaleToFit(100, 40);  // Adjust size
                    preparedByPara.Add(preparedSig);
                }
                else
                {
                    // fallback if file not found
                    preparedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
                }
                document.Add(preparedByPara);


                // === Checked By ===
                var checkedByPara = new Paragraph("Checked By: ")
                    .Add(new Text(voucher.CheckedBy ?? "").SetUnderline())
                    .Add("   Sign: ");

                string checkedBySignaturePath = Path.Combine("wwwroot", "signatures", "checked_by.png");
                if (System.IO.File.Exists(checkedBySignaturePath))
                {
                    var checkedSig = new Image(ImageDataFactory.Create(checkedBySignaturePath))
                        .ScaleToFit(100, 40);
                    checkedByPara.Add(checkedSig);
                }
                else
                {
                    checkedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
                }
                document.Add(checkedByPara);


                // === Approved By ===
                var approvedByPara = new Paragraph("Approved By: ")
                    .Add(new Text(voucher.ApprovedBy ?? "").SetUnderline())
                    .Add("   Sign: ");

                string approvedBySignaturePath = Path.Combine("wwwroot", "signatures", "approved_by.png");
                if (System.IO.File.Exists(approvedBySignaturePath))
                {
                    var approvedSig = new Image(ImageDataFactory.Create(approvedBySignaturePath))
                        .ScaleToFit(100, 40);
                    approvedByPara.Add(approvedSig);
                }
                else
                {
                    approvedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
                }
                document.Add(approvedByPara);


                // --- Footer ---
                var footerText = "Amtech Plaza, Forest Line, Off Ngong Road, Matasia Shopping Center\n" +
                                 "P. O. Box 79701 – 00200 Nairobi. Direct line +254 20 553300, Mobile: 0792716541, Fax +254 20 559867\n" +
                                 "Email: info@amtechafrica.com   Web: www.amtechafrica.com";

                document.ShowTextAligned(
                    new Paragraph(footerText).SetFontSize(9).SetTextAlignment(TextAlignment.CENTER),
                    PageSize.A4.GetWidth() / 2,
                    30,
                    pdf.GetNumberOfPages(),
                    TextAlignment.CENTER,
                    VerticalAlignment.BOTTOM,
                    0);

                document.Close();
                return File(ms.ToArray(), "application/pdf", $"Voucher_{voucher.VoucherNumber}.pdf");
            }
        }

        //Get: Generate Full Report
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GenerateFullReport(int id)
        {
            var voucher = await _context.PaymentVouchers
                .Include(v => v.Items)
                .Include(v => v.UploadedFile)
                .Include(v => v.Summaries)
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

            var stampFrame = new Div()
                .SetBorder(new DashedBorder(new DeviceRgb(173, 216, 230), 0.5f))
                .SetPadding(3)
                .SetWidth(150)
                .SetHeight(100)
                .SetTextAlignment(TextAlignment.CENTER);

            var stampText = new Paragraph()
                .Add(new Text("AMTECH TECHNOLOGIES LIMITED\n")
                    .SetFontSize(8).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)))
                .Add(new Text("P.O. BOX 79701-00200, NAIROBI\n")
                    .SetFontSize(8).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)))
                //.Add(new Text("DATE: ").SetFontSize(10).SetFontColor(new DeviceRgb(173, 216, 230)))
                //.Add(new Text($"{voucher.Date:dd/MM/yyyy}\n").SetFontSize(10).SetFontColor(new DeviceRgb(173, 216, 230)).SetUnderline())
                .Add(new Text("PAID")
                    .SetFontSize(20).SetBold().SetFontColor(new DeviceRgb(173, 216, 230)));

            doc.ShowTextAligned(
                stampText,
                450, 400, // position center
                pdf.GetNumberOfPages(),
                TextAlignment.CENTER,
                VerticalAlignment.MIDDLE,
                (float)(Math.PI / 6) // 👈 rotate 30°
            );

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
            var title = new Paragraph("Cheque/M-pesa/Cash Payment Voucher")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(18)
                .SetBold()
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
                //table.AddCell(i.ToString());
                table.AddCell(item.ItemNo.ToString());
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

            // --- Add Failed Section only if data exists ---
            if (!string.IsNullOrWhiteSpace(voucher.FailedDesc) || (voucher.VAT.HasValue && voucher.VAT.Value > 0))
            {
                Paragraph failedLine = new Paragraph()
                    .SetFontSize(11)
                    .Add(new Text(" "))
                    .Add(new Text(voucher.FailedDesc ?? "N/A")/*.SetUnderline(0.5f, -1)*/)
                    .Add(new Text("  ")) // small space
                    .Add(new Text(" and Total failed Amount: "))
                    .Add(new Text((voucher.VAT ?? 0).ToString("N2"))/*.SetUnderline(0.5f, -1)*/);

                doc.Add(failedLine);
            }

            // --- Amount in words ---
            Paragraph wordsLine = new Paragraph()
                .SetFontSize(11)
                .Add(new Text("Amount in words: "))
                .Add(new Text(voucher.AmountInWords ?? "").SetUnderline(0.5f, -1));
            doc.Add(wordsLine);

            // --- Payment Made in ---
            string paymentText = (!string.IsNullOrEmpty(voucher.PaymentMode) &&
                voucher.PaymentMode.Equals("M-pesa", StringComparison.OrdinalIgnoreCase))
                ? "Ksh B2C"
                : "Ksh (Cash)";

            Paragraph paymentLine = new Paragraph()
                .SetFontSize(11)
                .Add(new Text("Payment Made in: "))
                .Add(new Text(paymentText).SetUnderline(0.5f, -1));
            doc.Add(paymentLine);
            // --- Signature Section ---   
            // === Prepared By ===
            var preparedByPara = new Paragraph("\nPrepared By: ")
                .Add(new Text(voucher.PreparedBy ?? "").SetUnderline())
                .Add("   Sign: ");

            // Pick the correct image from wwwroot/signatures
            string preparedBySignaturePath = Path.Combine("wwwroot", "signatures", "prepared_by.png");
            if (System.IO.File.Exists(preparedBySignaturePath))
            {
                var preparedSig = new Image(ImageDataFactory.Create(preparedBySignaturePath))
                    .ScaleToFit(100, 40);  // Adjust size
                preparedByPara.Add(preparedSig);
            }
            else
            {
                // fallback if file not found
                preparedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
            }
            doc.Add(preparedByPara);


            // === Checked By ===
            var checkedByPara = new Paragraph("Checked By: ")
                .Add(new Text(voucher.CheckedBy ?? "").SetUnderline())
                .Add("   Sign: ");

            string checkedBySignaturePath = Path.Combine("wwwroot", "signatures", "checked_by.png");
            if (System.IO.File.Exists(checkedBySignaturePath))
            {
                var checkedSig = new Image(ImageDataFactory.Create(checkedBySignaturePath))
                    .ScaleToFit(100, 40);
                checkedByPara.Add(checkedSig);
            }
            else
            {
                checkedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
            }
            doc.Add(checkedByPara);


            // === Approved By ===
            var approvedByPara = new Paragraph("Approved By: ")
                .Add(new Text(voucher.ApprovedBy ?? "").SetUnderline())
                .Add("   Sign: ");

            string approvedBySignaturePath = Path.Combine("wwwroot", "signatures", "approved_by.png");
            if (System.IO.File.Exists(approvedBySignaturePath))
            {
                var approvedSig = new Image(ImageDataFactory.Create(approvedBySignaturePath))
                    .ScaleToFit(100, 40);
                approvedByPara.Add(approvedSig);
            }
            else
            {
                approvedByPara.Add(new Text("XXXXXXXXXXXXXXX").SetUnderline());
            }
            doc.Add(approvedByPara);


            // --- Footer ---
            var footerText = "Amtech Plaza, Forest Line, Off Ngong Road, Matasia Shopping Center\n" +
                                 "P. O. Box 79701 – 00200 Nairobi. Direct line +254 20 553300, Mobile: 0792716541, Fax +254 20 559867\n" +
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
                    // Venue Name Row
                    Table venueTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3 }))
                        .UseAllAvailableWidth();
                    venueTable.AddCell(new Cell().Add(new Paragraph("VENUE")));
                    venueTable.AddCell(new Cell().Add(new Paragraph(summary.Venue?.ToString())));
                    doc.Add(venueTable);

                    // Itemized Table
                    Table itemTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1, 1, 2, 1 }))
                        .UseAllAvailableWidth();

                    void AddRow(string label, int qty, decimal unitPrice, decimal total, bool showPaid)
                    {
                        itemTable.AddCell(new Paragraph(label));
                        itemTable.AddCell(new Paragraph(qty.ToString()));
                        itemTable.AddCell(new Paragraph(unitPrice.ToString("N0")));
                        itemTable.AddCell(new Paragraph(total.ToString("N2")));
                        itemTable.AddCell(new Paragraph(showPaid ? "PAID" : ""));
                    }

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

                    doc.Add(itemTable);
                    grandTotal += summary.SubTotalperVenue;
                }

                // ===== GRAND TOTAL =====
                Table sumTable = new Table(new float[] { 1, 3, 3 })
                    .UseAllAvailableWidth()
                    .SetFontSize(10);

                sumTable.AddCell(new Cell(4, 2).Add(new Paragraph("GRAND TOTAL").SetBold()));
                sumTable.AddCell(new Cell(1, 2).Add(new Paragraph(grandTotal.ToString("N2")).SetBold()));
                doc.Add(sumTable);

                // 📌 Total Failed Amount & Description
                decimal totalFailedAmount = voucher.Summaries.Sum(s => s.FailedAmt ?? 0);
                if (totalFailedAmount > 0)
                {
                    string failedDescriptions = string.Join("; ", voucher.Summaries
                        .Where(s => !string.IsNullOrEmpty(s.FailedDesc))
                        .Select(s => s.FailedDesc));

                    doc.Add(new Paragraph("\n"));
                    doc.Add(new Paragraph($"Total Failed Amount: {totalFailedAmount:N2}"));

                    if (!string.IsNullOrEmpty(failedDescriptions))
                    {
                        doc.Add(new Paragraph($"Failed Description: {failedDescriptions}"));
                    }

                    decimal netTotal = grandTotal - totalFailedAmount;
                    doc.Add(new Paragraph($"Net Total (After Failed): {netTotal:N2}"));
                }
            }

            // === Uploaded Files Section ===
            if (voucher.UploadedFile != null && voucher.UploadedFile.Any())
            {
                // ✅ Add page break only once BEFORE starting file output
                //doc.Add(new AreaBreak());

                var orderedFiles = voucher.UploadedFile
                    .OrderBy(f =>
                    {
                        var ext = Path.GetExtension(f.FilePath).ToLowerInvariant();
                        return ext switch
                        {
                            ".pdf" => 1,
                            ".xlsx" => 2,
                            ".docx" => 3,
                            ".txt" => 4,
                            ".jpg" or ".jpeg" or ".png" => 5,
                            _ => 6
                        };
                    })
                    .ThenBy(f => f.UploadedOn)
                    .ToList();

                foreach (var file in orderedFiles)
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
                        using var srcPdf = new PdfDocument(reader);
                        srcPdf.CopyPagesTo(1, srcPdf.GetNumberOfPages(), pdf);
                    }
                    else if (ext == ".docx")
                    {
                        string text = ExtractTextFromDocx(physicalPath);
                        doc.Add(new Paragraph(text).SetFontSize(11));
                    }
                    else if (ext == ".txt")
                    {
                        string text = await System.IO.File.ReadAllTextAsync(physicalPath);
                        doc.Add(new Paragraph(text).SetFontSize(11));
                    }
                    else if (ext == ".xlsx")
                    {
                        using var excelMs = new MemoryStream();
                        using (var excelDoc = new PdfSharpCore.Pdf.PdfDocument())
                        {
                            AddExcelAsPdfTable(physicalPath, excelDoc);
                            excelDoc.Save(excelMs, false);
                        }

                        excelMs.Position = 0;

                        using var excelReader = new PdfReader(excelMs);
                        using var excelPdf = new iText.Kernel.Pdf.PdfDocument(excelReader);
                        excelPdf.CopyPagesTo(1, excelPdf.GetNumberOfPages(), pdf);
                    }
                }
            }

            // Close main document
            doc.Close();

            //Return final PDF
            return File(ms.ToArray(), "application/pdf", $"Voucher_{voucher.VoucherNumber}_FullReport.pdf");
        }

        // Add page numbers in format "1 / 10" to every page
        private void AddPageNumbers(MemoryStream ms)
        {
            ms.Position = 0;

            using var finalStream = new MemoryStream();
            using var reader = new PdfReader(ms);
            using var writer = new PdfWriter(finalStream);
            using var pdfDoc = new PdfDocument(reader, writer);

            int totalPages = pdfDoc.GetNumberOfPages();

            for (int i = 1; i <= totalPages; i++)
            {
                var pageSize = pdfDoc.GetPage(i).GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfDoc.GetPage(i));

                new iText.Layout.Canvas(canvas, pageSize)
                    .ShowTextAligned(
                        new Paragraph($"{i} / {totalPages}")
                            .SetFontSize(10)
                            .SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY),
                        pageSize.GetWidth() / 2,
                        20, // distance from bottom
                        i,
                        TextAlignment.CENTER,
                        VerticalAlignment.BOTTOM,
                        0
                    );
            }

            pdfDoc.Close();

            ms.SetLength(0);
            finalStream.Position = 0;
            finalStream.CopyTo(ms);
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

        private void AddTextAsPdfPages(string text, PdfSharpCore.Pdf.PdfDocument outputDoc)
        {
            const int approxCharsPerPage = 3000;
            var chunks = ChunkText(text, approxCharsPerPage);
            var font = new XFont("Arial", 12);

            foreach (var chunk in chunks)
            {
                var page = outputDoc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                using var gfx = XGraphics.FromPdfPage(page);
                var tf = new XTextFormatter(gfx);
                var rect = new XRect(40, 40, page.Width - 80, page.Height - 80);
                tf.DrawString(chunk, font, XBrushes.Black, rect);
            }
        }

        private static IEnumerable<string> ChunkText(string text, int chunkSize)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            int index = 0;
            while (index < text.Length)
            {
                int length = Math.Min(chunkSize, text.Length - index);
                yield return text.Substring(index, length);
                index += length;
            }
        }


        private void AddExcelAsPdfTable(string path, PdfSharpCore.Pdf.PdfDocument outputDoc)
        {
            using var stream = System.IO.File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

            double margin = 40;

            do
            {
                // Read all rows into memory
                var table = new List<List<string>>();
                while (reader.Read())
                {
                    var row = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        row.Add(reader.GetValue(i)?.ToString() ?? "");
                    table.Add(row);
                }

                if (table.Count == 0) continue;

                int colCount = table.Max(r => r.Count);

                // 1. Calculate column widths
                double[] colWidths = new double[colCount];
                using (var measureGfx = XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards))
                {
                    var font = new XFont("Arial", 9);
                    for (int c = 0; c < colCount; c++)
                    {
                        double maxWidth = 40; // minimum
                        foreach (var row in table)
                        {
                            if (c < row.Count)
                            {
                                string text = row[c];
                                var size = measureGfx.MeasureString(text, font);
                                if (size.Width + 10 > maxWidth)
                                    maxWidth = size.Width + 10;
                            }
                        }
                        colWidths[c] = maxWidth;
                    }
                }

                // Fit to page width
                double totalWidth = colWidths.Sum();
                double pageWidth = outputDoc.Pages.Count == 0 ? 842 : outputDoc.Pages[0].Width; // A4 landscape width
                double maxTableWidth = pageWidth - 2 * margin;

                if (totalWidth > maxTableWidth)
                {
                    double scale = maxTableWidth / totalWidth;
                    for (int i = 0; i < colWidths.Length; i++)
                        colWidths[i] *= scale;
                }

                // 2. Render table
                var page = outputDoc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                var gfx = XGraphics.FromPdfPage(page);

                double startY = margin;

                foreach (var row in table)
                {
                    // ---- Compute row height dynamically ----
                    double maxRowHeight = 20; // default
                    for (int c = 0; c < colCount; c++)
                    {
                        string text = c < row.Count ? row[c] : "";
                        if (string.IsNullOrEmpty(text)) continue;

                        double cellWidth = colWidths[c] - 4;
                        int fontSize = 9;
                        var font = new XFont("Arial", fontSize);

                        // shrink font if needed
                        while (gfx.MeasureString(text, font).Width > cellWidth && fontSize > 6)
                        {
                            fontSize--;
                            font = new XFont("Arial", fontSize);
                        }

                        // estimate wrapped height
                        var lines = WrapText(gfx, text, font, cellWidth);
                        double rowHeight = lines.Count * (gfx.MeasureString("Test", font).Height + 2) + 4;

                        if (rowHeight > maxRowHeight)
                            maxRowHeight = rowHeight;
                    }

                    // ---- Page break if needed ----
                    if (startY + maxRowHeight > page.Height - margin)
                    {
                        page = outputDoc.AddPage();
                        page.Size = PdfSharpCore.PageSize.A4;
                        page.Orientation = PdfSharpCore.PageOrientation.Landscape;
                        gfx = XGraphics.FromPdfPage(page);
                        startY = margin;
                    }

                    // ---- Draw row ----
                    double startX = margin;
                    for (int c = 0; c < colCount; c++)
                    {
                        string text = c < row.Count ? row[c] : "";
                        double cellWidth = colWidths[c];
                        var rect = new XRect(startX, startY, cellWidth, maxRowHeight);
                        gfx.DrawRectangle(XPens.Black, rect);

                        if (!string.IsNullOrEmpty(text))
                        {
                            int fontSize = 9;
                            var font = new XFont("Arial", fontSize);

                            // shrink font if needed
                            while (gfx.MeasureString(text, font).Width > cellWidth - 4 && fontSize > 6)
                            {
                                fontSize--;
                                font = new XFont("Arial", fontSize);
                            }

                            // wrap text (with mid-word breaking support)
                            var lines = WrapText(gfx, text, font, cellWidth - 4);

                            // draw lines
                            double lineHeight = gfx.MeasureString("Test", font).Height + 2;
                            double lineY = rect.Y + 2;
                            foreach (var line in lines)
                            {
                                gfx.DrawString(line, font, XBrushes.Black,
                                    new XRect(rect.X + 2, lineY, rect.Width - 4, lineHeight),
                                    XStringFormats.TopLeft);
                                lineY += lineHeight;
                            }
                        }

                        startX += cellWidth;
                    }

                    startY += maxRowHeight;
                }

            } while (reader.NextResult());
        }

        
        /// Wraps text into lines that fit inside a given width. 
        /// Supports word-wrap and mid-word breaking.
        private List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            List<string> lines = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return lines;

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var size = gfx.MeasureString(testLine, font);

                if (size.Width > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                        lines.Add(currentLine);

                    // if the word itself is too long, break it mid-word
                    if (gfx.MeasureString(word, font).Width > maxWidth)
                    {
                        string chunk = "";
                        foreach (char ch in word)
                        {
                            var testChunk = chunk + ch;
                            if (gfx.MeasureString(testChunk, font).Width > maxWidth)
                            {
                                lines.Add(chunk);
                                chunk = ch.ToString();
                            }
                            else
                            {
                                chunk = testChunk;
                            }
                        }
                        if (!string.IsNullOrEmpty(chunk))
                            currentLine = chunk;
                        else
                            currentLine = "";
                    }
                    else
                    {
                        currentLine = word;
                    }
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines;
        }
    }
}
