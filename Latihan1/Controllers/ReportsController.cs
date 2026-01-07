using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    [Route("reports")]
    public class ReportsController : Controller
    {
        private readonly DapperDb _db;
        public ReportsController(DapperDb db) => _db = db;

        // ====== VIEW: Halaman ringkas "Laporan" ======
        // (pakai View yang sudah kamu punya: Views/AdminPage/Laporan.cshtml)
        [HttpGet("laporan")]
        public IActionResult Index() => View("~/Views/AdminPage/Laporan.cshtml");

        // ====== VIEW: Lihat laporan lengkap (tabel HTML) ======
        [HttpGet("view")]
        public async Task<IActionResult> ViewAll()
        {
            var rows = await _db.GetAllJadwalAsync((int?)null);
            return View("~/Views/AdminPage/LaporanView.cshtml", rows);
        }

        // ====== EXPORT: CSV ======
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv()
        {
            var rows = await _db.GetAllJadwalAsync((int?)null);

            var sb = new StringBuilder();
            sb.AppendLine("Hari,Waktu Mulai,Waktu Selesai,Mapel,Guru,Kelas/Ruangan");

            string Day(int d) => new[] { "-", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" }[Math.Clamp(d, 0, 7)];

            foreach (var r in rows)
            {
                var line = string.Join(",",
                    Day(r.Hari),
                    r.Mulai.ToString(@"hh\:mm"),
                    r.Selesai.ToString(@"hh\:mm"),
                    Csv(r.Mapel),
                    Csv(r.GuruNama),
                    Csv($"{r.KelasNama}{(string.IsNullOrWhiteSpace(r.Ruangan) ? "" : $" / {r.Ruangan}")}")
                );
                sb.AppendLine(line);
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"jadwal-sekolah-{DateTime.Now:yyyyMMdd}.csv");

            static string Csv(string? s)
            {
                s ??= "";
                // escape tanda kutip ganda
                s = s.Replace("\"", "\"\"");
                // bungkus kalau ada koma / kutip
                return (s.Contains(',') || s.Contains('"')) ? $"\"{s}\"" : s;
            }
        }

        // ====== EXPORT: PDF (QuestPDF) ======
        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf()
        {
            var rows = (await _db.GetAllJadwalAsync((int?)null))
                       .OrderBy(r => r.Hari).ThenBy(r => r.Mulai)
                       .ToList();

            string Day(int d) => new[] { "-", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" }[Math.Clamp(d, 0, 7)];

            var title = $"Laporan Jadwal • {DateTime.Now:dd MMM yyyy}";

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);
                    page.Size(PageSizes.A4);

                    page.Header().Element(h =>
                    {
                        h.Text(title)
                         .SemiBold().FontSize(16);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        // kolom
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);   // Hari
                            cols.RelativeColumn(1.2f);// Waktu
                            cols.RelativeColumn(1.5f);// Mapel
                            cols.RelativeColumn(1.8f);// Guru
                            cols.RelativeColumn(1.4f);// Kelas/Ruangan
                        });

                        // header
                        table.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Hari");
                            h.Cell().Element(CellHeader).Text("Waktu");
                            h.Cell().Element(CellHeader).Text("Mapel");
                            h.Cell().Element(CellHeader).Text("Guru");
                            h.Cell().Element(CellHeader).Text("Kelas / Ruangan");

                            static IContainer CellHeader(IContainer c) =>
                                c.DefaultTextStyle(x => x.SemiBold())
                                 .PaddingVertical(6).PaddingHorizontal(8)
                                 .BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        });

                        // isi
                        foreach (var r in rows)
                        {
                            table.Cell().Element(Cell).Text(Day(r.Hari));
                            table.Cell().Element(Cell).Text($"{r.Mulai:hh\\:mm} - {r.Selesai:hh\\:mm}");
                            table.Cell().Element(Cell).Text(r.Mapel);
                            table.Cell().Element(Cell).Text(r.GuruNama);
                            table.Cell().Element(Cell).Text($"{r.KelasNama}{(string.IsNullOrWhiteSpace(r.Ruangan) ? "" : $" / {r.Ruangan}")}");

                            static IContainer Cell(IContainer c) =>
                                c.PaddingVertical(6).PaddingHorizontal(8)
                                 .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten4);
                        }
                    });

                    page.Footer().AlignRight().Text(txt =>
                    {
                        txt.Span("Hal. ");
                        txt.CurrentPageNumber();
                        txt.Span(" / ");
                        txt.TotalPages();
                    });
                });
            });

            var pdfBytes = doc.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"jadwal-sekolah-{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}
