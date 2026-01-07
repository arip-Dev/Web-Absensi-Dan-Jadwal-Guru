using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure; // for Settings.License
using System.Text;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class KelasController : Controller
    {
        private readonly DapperDb _db;
        public KelasController(DapperDb db) => _db = db;

        // Helper mapping
        private static KelasModel Map(DapperDb.KelasRow r) => new KelasModel
        {
            Id = r.Id,
            Tingkat = r.Tingkat,
            Nama = r.Nama,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt
        };

        // GET /Kelas  atau /Kelas?tingkat=X
        [HttpGet]
        public async Task<IActionResult> Index(string? tingkat)
        {
            // === Selalu isi ViewData["Groups"] dengan tipe yang BENAR ===
            var groupRows = await _db.GetKelasGroupsAsync(); // SELECT Tingkat, COUNT(*)
            var groupVm = groupRows.Select(g => new KelasGroupVm
            {
                Tingkat = g.Tingkat,
                Count = g.Count
            }).ToList();
            ViewData["Groups"] = groupVm;

            if (string.IsNullOrWhiteSpace(tingkat))
            {
                // Halaman grid X / XI / XII
                var vm = new KelasListVm
                {
                    Tingkat = null,
                    Items = Enumerable.Empty<KelasModel>()
                };
                return View("~/Views/Adminpage/Kelas/Index.cshtml", vm);
            }

            // Halaman daftar kelas per tingkat
            var rows = await _db.GetKelasByGradeAsync(tingkat);
            var items = rows.Select(Map);

            return View(
                "~/Views/Adminpage/Kelas/Index.cshtml",
                new KelasListVm { Tingkat = tingkat, Items = items }
            );
        }

        // GET /Kelas/Create?tingkat=X
        [HttpGet]
        public IActionResult Create(string? tingkat = "X")
        {
            var m = new KelasModel { Tingkat = tingkat ?? "X", IsActive = true };
            return View("~/Views/Adminpage/Kelas/Create.cshtml", m);
        }

        // POST /Kelas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KelasModel m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/Kelas/Create.cshtml", m);

            try
            {
                await _db.InsertKelasAsync(m.Tingkat, m.Nama, m.IsActive);
                TempData["ok"] = "Kelas berhasil ditambahkan.";
                return RedirectToAction(nameof(Index), new { tingkat = m.Tingkat });
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                ModelState.AddModelError("", "Kelas dengan tingkat & nama tersebut sudah ada.");
                return View("~/Views/Adminpage/Kelas/Create.cshtml", m);
            }
        }

        // GET /Kelas/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var row = await _db.GetKelasByIdAsync(id);
            if (row is null) return NotFound();

            var m = new KelasModel
            {
                Id = row.Id,
                Tingkat = row.Tingkat,
                Nama = row.Nama,
                IsActive = row.IsActive,
                CreatedAt = row.CreatedAt
            };
            return View("~/Views/Adminpage/Kelas/Edit.cshtml", m);
        }

        // POST /Kelas/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(KelasModel m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/Kelas/Edit.cshtml", m);

            await _db.UpdateKelasAsync(m.Id, m.Tingkat, m.Nama, m.IsActive);
            TempData["ok"] = "Perubahan disimpan.";
            return RedirectToAction(nameof(Index), new { tingkat = m.Tingkat });
        }

        // POST /Kelas/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string tingkat)
        {
            try
            {
                await _db.DeleteKelasAsync(id);
                TempData["ok"] = "Kelas dihapus.";
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                TempData["err"] = "Kelas tidak dapat dihapus karena sedang dipakai data lain.";
            }
            return RedirectToAction(nameof(Index), new { tingkat });
        }

        // GET /Kelas/Jadwal/5  (id = KelasId)
        [HttpGet]
        public async Task<IActionResult> Jadwal(int id)
        {
            var k = await _db.GetKelasByIdAsync(id);
            if (k is null) return NotFound();

            var items = await _db.ListJadwalByKelasAsync(id);

            var vm = new KelasJadwalVm
            {
                Kelas = new KelasModel
                {
                    Id = k.Id,
                    Tingkat = k.Tingkat,
                    Nama = k.Nama,
                    IsActive = k.IsActive,
                    CreatedAt = k.CreatedAt
                },
                Items = items
            };

            return View("~/Views/Adminpage/Kelas/Jadwal.cshtml", vm);
        }
        private static string DayName(int d)
        {
            return d switch
            {
                1 => "Senin",
                2 => "Selasa",
                3 => "Rabu",
                4 => "Kamis",
                5 => "Jumat",
                6 => "Sabtu",
                7 => "Minggu",
                _ => "-"
            };
        }

        [HttpGet]
        public async Task<IActionResult> DownloadCsv(int id)
        {
            var kelas = await _db.GetKelasByIdAsync(id);
            if (kelas is null) return NotFound();

            var items = await _db.ListJadwalByKelasAsync(id);
            var ordered = items.OrderBy(x => x.Hari).ThenBy(x => x.Mulai).ToList();

            var sb = new StringBuilder();
            // header
            sb.AppendLine("Hari,Waktu,Mapel,Guru,Ruangan");
            foreach (var r in ordered)
            {
                var hari = DayName(r.Hari);
                var waktu = $"{r.Mulai:hh\\:mm}-{r.Selesai:hh\\:mm}";
                // CSV-safe (quote jika ada koma)
                static string Q(string s) => s?.Contains(',') == true ? $"\"{s.Replace("\"", "\"\"")}\"" : s ?? "";
                sb.AppendLine($"{Q(hari)},{Q(waktu)},{Q(r.Mapel)},{Q(r.GuruNama)},{Q(r.Ruangan ?? "")}");
            }

            var fileName = $"Jadwal_{kelas.Tingkat}-{kelas.Nama}.csv";
            // Tambahkan BOM agar enak dibuka di Excel
            var bom = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(sb.ToString());
            var bytes = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
            Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);

            return File(bytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var kelas = await _db.GetKelasByIdAsync(id);
            if (kelas is null) return NotFound();

            var items = await _db.ListJadwalByKelasAsync(id);
            var ordered = items.OrderBy(x => x.Hari).ThenBy(x => x.Mulai).ToList();

            // QuestPDF perlu set lisensi Community
            //Settings.License = LicenseType.Community;

            var title = $"Jadwal Kelas {kelas.Tingkat}-{kelas.Nama}";
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(36);
                    page.Size(PageSizes.A4);

                    page.Header().Text(title).SemiBold().FontSize(16);
                    page.Content().PaddingTop(8).Table(table =>
                    {
                        // kolom
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);   // Hari
                            cols.RelativeColumn(1.2f);// Waktu
                            cols.RelativeColumn(1.5f);// Mapel
                            cols.RelativeColumn(1.5f);// Guru
                            cols.RelativeColumn(1.0f);// Ruangan
                        });

                        // header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Hari");
                            header.Cell().Element(CellHeader).Text("Waktu");
                            header.Cell().Element(CellHeader).Text("Mapel");
                            header.Cell().Element(CellHeader).Text("Guru");
                            header.Cell().Element(CellHeader).Text("Ruangan");

                            static IContainer CellHeader(IContainer c) =>
                                c.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(6).PaddingHorizontal(6)
                                 .Background(Colors.Grey.Lighten3).Border(1).BorderColor(Colors.Grey.Lighten2);
                        });

                        foreach (var r in ordered)
                        {
                            var hari = DayName(r.Hari);
                            var waktu = $"{r.Mulai:hh\\:mm} - {r.Selesai:hh\\:mm}";

                            table.Cell().Element(Cell).Text(hari);
                            table.Cell().Element(Cell).Text(waktu);
                            table.Cell().Element(Cell).Text(r.Mapel ?? "");
                            table.Cell().Element(Cell).Text(r.GuruNama ?? "");
                            table.Cell().Element(Cell).Text(r.Ruangan ?? "");

                            static IContainer Cell(IContainer c) =>
                                c.PaddingVertical(6).PaddingHorizontal(6)
                                 .BorderBottom(1).BorderColor(Colors.Grey.Lighten3);
                        }
                    });

                    page.Footer().AlignRight().Text(txt =>
                    {
                        txt.Span("Generated ").Light();
                        txt.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm"));
                    });
                });
            }).GeneratePdf();

            var fileName = $"Jadwal_{kelas.Tingkat}-{kelas.Nama}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

    }
}
