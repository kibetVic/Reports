using Reports.Data;
using Reports.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Reports.Data;


namespace Ivoices.Controllers
{
    [Authorize]
    public class InvoicesController : Controller
    {
        private readonly ReportsDbContext _context;
        private readonly object _document;

        public InvoicesController(ReportsDbContext context)
        {
            _context = context;
            _document = _document;
        }

        // GET: /Invoices
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Items)
                .OrderByDescending(i => i.TrxDate)
                .ToListAsync();

            return View(invoices);
        }

        // GET: /Invoices/Details/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            return View(invoice);
        }

        // GET: /Invoices/Create
        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            var model = new Invoice
            {
                TrxDate = DateTime.Now,
                Items = new List<InvoiceItem> { new InvoiceItem() }
            };
            return View(model);
        }


        // POST: /Invoices/Create
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice invoice)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // Log and show model validation issues
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    ModelState.AddModelError("", $"Validation failed: {errors}");
                    return View(invoice);
                }

                // ✅ Generate unique Invoice Number before saving
                invoice.InvoiceNumber = await GenerateInvoiceNumberAsync();

                // Compute totals
                invoice.SubTotal = invoice.Items?.Sum(i => i.Quantity * i.Rate) ?? 0;
                invoice.VatTotal = invoice.Items?.Sum(i => i.VatAmount) ?? 0;
                invoice.TotalAmount = (invoice.SubTotal ?? 0) + (invoice.VatTotal ?? 0) - (invoice.Credits ?? 0);

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Invoice saved successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", $"Database error: {ex.InnerException?.Message ?? ex.Message}");
                return View(invoice);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Unexpected error: {ex.Message}");
                return View(invoice);
            }
        }


        // GET: /Invoices/Edit/5
       [Authorize]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            return View(invoice);
        }


        // POST: /Invoices/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Invoice invoice)
        {
            if (id != invoice.InvoiceId)
            {
                ModelState.AddModelError("", "Invoice ID mismatch.");
                return View(invoice);
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = string.Join("; ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage));

                    ModelState.AddModelError("", $"Validation failed: {errors}");
                    return View(invoice);
                }

                var existing = await _context.Invoices
                    .Include(i => i.Items)
                    .FirstOrDefaultAsync(i => i.InvoiceId == id);

                if (existing == null)
                {
                    ModelState.AddModelError("", "Invoice not found in database.");
                    return View(invoice);
                }

                // 🔁 Remove old items & update list
                _context.InvoiceItems.RemoveRange(existing.Items);
                existing.Items = invoice.Items;

                // 🔁 Update basic fields
                existing.VATREGNO = invoice.VATREGNO;
                existing.TrxDate = invoice.TrxDate;
                existing.InvoiceTo = invoice.InvoiceTo;
                existing.Project = invoice.Project;
                existing.Terms = invoice.Terms;
                existing.PONO = invoice.PONO;
                existing.Credits = invoice.Credits;

                // ✅ Keep existing InvoiceNumber OR regenerate if empty
                if (string.IsNullOrEmpty(existing.InvoiceNumber))
                {
                    var lastInvoice = await _context.Invoices
                        .OrderByDescending(i => i.InvoiceId)
                        .FirstOrDefaultAsync();

                    int nextNumber = 100; // Default start number
                    if (lastInvoice != null && !string.IsNullOrEmpty(lastInvoice.InvoiceNumber))
                    {
                        // Extract the numeric part (e.g., "0100" from "AMT01002025")
                        var numericPart = new string(lastInvoice.InvoiceNumber
                            .SkipWhile(c => !char.IsDigit(c))
                            .TakeWhile(char.IsDigit)
                            .ToArray());

                        if (int.TryParse(numericPart.Substring(0, 4), out int currentNum))
                            nextNumber = currentNum + 1;
                    }

                    var year = DateTime.Now.Year;
                    existing.InvoiceNumber = $"AMT{nextNumber:D4}{year}";
                }

                // 🔁 Recalculate totals
                existing.SubTotal = invoice.Items?.Sum(i => i.Quantity * i.Rate) ?? 0;
                existing.VatTotal = invoice.Items?.Sum(i => i.VatAmount) ?? 0;
                existing.TotalAmount = (existing.SubTotal ?? 0) + (existing.VatTotal ?? 0) - (existing.Credits ?? 0);

                await _context.SaveChangesAsync();

                TempData["Success"] = "Invoice updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", $"Database error: {ex.InnerException?.Message ?? ex.Message}");
                return View(invoice);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Unexpected error: {ex.Message}");
                return View(invoice);
            }
        }

        [Authorize]
        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var currentYear = DateTime.Now.Year.ToString();

            // Get the last invoice for this year
            var lastInvoice = await _context.Invoices
                .Where(i => i.InvoiceNumber.EndsWith(currentYear))
                .OrderByDescending(i => i.InvoiceId)
                .FirstOrDefaultAsync();

            int nextNumber = 100; // Start from 0100 if none exist

            if (lastInvoice != null && !string.IsNullOrEmpty(lastInvoice.InvoiceNumber))
            {
                // Extract the numeric part: AMT01002025 => 0100
                var numPart = lastInvoice.InvoiceNumber.Substring(3, 4);
                if (int.TryParse(numPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            return $"AMT{nextNumber:D4}{currentYear}";
        }





        // GET: Invoices1/Delete/5
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // POST: Invoices1/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceId == id);
        }



        //[Authorize]
        [HttpGet]
        public async Task<IActionResult> GeneratePdf(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null)
                return NotFound();

            QuestPDF.Settings.License = LicenseType.Community;

            string footerText = "Amtech Plaza, Forest Line, Off Ngong Road, Matasia Shopping Center,  P. O. Box 79701 – 00200 Nairobi.\n" +
                        "Email: info@amtechafrica.com |  Web: www.amtechafrica.com |  Mobile: 0792716541 / 0734871556";

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Times New Roman"));

                    // === Add the STAMP on the page background ===
                    page.Foreground().AlignCenter().AlignMiddle().Element(e =>
                    {
                        e.TranslateY(50) // 👈 move stamp 30px below center
                         .TranslateX(-30)
                         .Rotate(-20)
                         .Padding(6)
                         .Width(160)
                         .Height(100)
                         .AlignCenter()
                         .AlignMiddle()
                         .Column(c =>
                         {
                             c.Item().Text("AMTECH TECHNOLOGIES LIMITED")
                                 .FontSize(8).Bold().FontColor("#ADD8E6").AlignCenter();

                             c.Item().Text("P.O. BOX 79701-00200, NAIROBI")
                                 .FontSize(8).Bold().FontColor("#ADD8E6").AlignCenter();

                             c.Item().PaddingTop(5)
                              .AlignCenter()
                              .Text(text =>
                              {
                                  text.Span("DATE: ").FontSize(12).Bold().FontColor("#ADD8E6");
                                  text.Span(invoice.TrxDate.ToString("dd/MM/yyyy"))
                                      .FontSize(12).Bold().FontColor("#ADD8E6");
                              });
                         });
                    });


                    //page.Content().Column(col =>
                    page.Content().PaddingTop(40).Column(col =>
                    {
                        // ================= HEADER =================
                        col.Item().Padding(15).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3); // Left column
                                cols.RelativeColumn(2); // Right column
                            });

                            // LEFT - Company Info
                            table.Cell().Column(column =>
                            {
                                column.Item().Text("AMTECH TECHNOLOGIES LTD").Bold().FontSize(12);
                                column.Item().Text("P.O BOX 79701-00200");
                                column.Item().Text("NAIROBI");
                                column.Item().Text("Mobile: 0792716541/0734871556");
                                column.Item().Text("info@amtechafrica.com").FontColor("#0070C0");
                            });

                            // RIGHT - Invoice Info
                            table.Cell().AlignRight().Column(column =>
                            {
                                column.Item().AlignCenter().Text("Invoice").Bold().FontSize(14);

                                column.Item().Table(t =>
                                {
                                    t.ColumnsDefinition(cd =>
                                    {
                                        cd.RelativeColumn(1);
                                        cd.RelativeColumn(1);
                                        cd.RelativeColumn(1);
                                    });

                                    // Header Row
                                    t.Header(header =>
                                    {
                                        header.Cell().Border(1).Padding(3).AlignCenter().Text("VAT REG NO").Bold();
                                        header.Cell().Border(1).Padding(3).AlignCenter().Text("Trx Date").Bold();
                                        header.Cell().Border(1).Padding(3).AlignCenter().Text("Invoice").Bold();
                                    });

                                    // Data Row
                                    t.Cell().Border(1).Padding(3).AlignCenter().Text(invoice.VATREGNO ?? "-");
                                    t.Cell().Border(1).Padding(3).AlignCenter().Text(invoice.TrxDate.ToString("dd/MM/yyyy"));
                                    t.Cell().Border(1).Padding(3).AlignCenter().Text(invoice.InvoiceNumber ?? "-");
                                });
                            });
                        });

                        // ========== INVOICE TO ==========
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1); // Left 1/3
                                cd.RelativeColumn(2); // Empty right 2/3
                            });

                            // "Invoice To" cell on the left
                            t.Cell().Border(1).Padding(5).Column(c =>
                            {
                                c.Item().Text("Invoice To:").Bold();
                                c.Item().Text(invoice.InvoiceTo ?? "");
                            });

                            // Empty cell (takes up the remaining 2/3 of the row)
                            t.Cell();
                        });


                        // ========== P.O.NO. / Terms / Project ==========
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(1); // Left 1/3 (empty)
                                cd.RelativeColumn(2); // Right 2/3 (content)
                            });

                            // Empty left cell (1/3 space)
                            t.Cell();

                            // Right cell (2/3 width)
                            t.Cell().Padding(10).Table(inner =>
                            {
                                inner.ColumnsDefinition(cd =>
                                {
                                    cd.RelativeColumn(1);
                                    cd.RelativeColumn(1);
                                    cd.RelativeColumn(1);
                                });

                                inner.Header(h =>
                                {
                                    h.Cell().Border(1).Padding(4).Text("P.O.NO.").Bold();
                                    h.Cell().Border(1).Padding(4).Text("Terms").Bold();
                                    h.Cell().Border(1).Padding(4).Text("Project").Bold();
                                });

                                inner.Cell().Border(1).Padding(4).Text(invoice.PONO ?? "");
                                inner.Cell().Border(1).Padding(4).Text(invoice.Terms ?? "");
                                inner.Cell().Border(1).Padding(4).Text(invoice.Project ?? "");
                            });
                        });

                        // ========== ITEMS TABLE ==========
                        col.Item().Padding(10).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(7); // Description (wider)
                                cd.RelativeColumn(2); // Qty
                                cd.RelativeColumn(3); // Rate
                                cd.RelativeColumn(3); // VAT AMT
                                cd.RelativeColumn(3); // Amount
                            });

                            t.Header(h =>
                            {
                                h.Cell().Border(1).Padding(6).Text("Description").Bold();
                                h.Cell().Border(1).Padding(6).AlignCenter().Text("QTY").Bold();
                                h.Cell().Border(1).Padding(6).AlignCenter().Text("Rate").Bold();
                                h.Cell().Border(1).Padding(6).AlignCenter().Text("VAT AMT").Bold();
                                h.Cell().Border(1).Padding(6).AlignRight().Text("Amount KES").Bold();
                            });

                            foreach (var item in invoice.Items)
                            {
                                t.Cell().Border(1).Padding(6).Text(item.Description ?? "");
                                t.Cell().Border(1).Padding(6).AlignCenter().Text(item.Quantity.ToString());
                                t.Cell().Border(1).Padding(6).AlignRight().Text($"{item.Rate:N2}");
                                t.Cell().Border(1).Padding(6).AlignRight().Text($"{item.VatAmount:N2}");
                                t.Cell().Border(1).Padding(6).AlignRight().Text($"{item.Amount:N2}");
                            }
                        });


                        // ========== FOOTER SECTION ==========
                        col.Item().Padding(10).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(3); // Left
                                cd.RelativeColumn(2); // Right
                            });

                            // LEFT - Bank details
                            t.Cell().Border(1).Padding(5).Column(left =>
                            {

                                left.Item().PaddingTop(5).Text("BANK DETAILS:").Bold();
                                left.Item().Text($"NAME: {invoice.BankName}");
                                left.Item().Text($"BRANCH: {invoice.BankBranch}");
                                left.Item().Text($"ACCOUNT: {invoice.BankAccount}");

                                left.Item().PaddingTop(5).Text("MPESA PAYMENT").Bold();
                                left.Item().Text($"PAYBILL: {invoice.MpesaPaybill}");
                                left.Item().Text($"ACCOUNT: {invoice.MpesaAccount}");
                            });

                            // RIGHT - Totals
                            t.Cell().Border(1).Column(right =>
                            {
                                right.Item().BorderBottom(1).Padding(4).Row(r =>
                                {
                                    r.RelativeColumn().Text("SUBTOTAL").Bold();
                                    r.ConstantColumn(90).AlignRight().Text($"{invoice.SubTotal?.ToString("N2") ?? "0.00"}");
                                });

                                right.Item().BorderBottom(1).Padding(4).Row(r =>
                                {
                                    r.RelativeColumn().Text("VAT total");
                                    r.ConstantColumn(90).AlignRight().Text($"{invoice.VatTotal?.ToString("N2") ?? "-"}");
                                });

                                right.Item().BorderBottom(1).Padding(4).Row(r =>
                                {
                                    r.RelativeColumn().Text("Credits");
                                    r.ConstantColumn(90).AlignRight().Text($"{invoice.Credits?.ToString("N2") ?? "0.00"}");
                                });

                                right.Item().Padding(4).Row(r =>
                                {
                                    r.RelativeColumn().Text("TOTAL").Bold();
                                    r.ConstantColumn(90).AlignRight().Text($"{invoice.TotalAmount?.ToString("N2") ?? "0.00"}").Bold();
                                });
                            });
                        });
                    });

                    // === FOOTER TEXT (always at bottom of page) ===
                    page.Footer().AlignCenter().PaddingTop(10).Text(footerText)
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1)
                        .AlignCenter();
                });

            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Invoice_{invoice.InvoiceNumber}.pdf");
        }

    }
}
