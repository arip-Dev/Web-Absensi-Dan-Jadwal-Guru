using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure; // for Settings.License
using System.Text;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class kelasController : Controller
    {
        private readonly DapperDb _db;
        public kelasController(DapperDb db) => _db = db;

        // Helper mapping
        private static kelasModel Map(DapperDb.KelasRow r) => new kelasModel
        {
            id = r.Id,
            tingkat = r.Tingkat,
            nama = r.Nama,
            isactive = r.IsActive,
            createdat = r.CreatedAt
        };

        // GET /kelas  atau /kelas?tingkat=X
        [HttpGet]
        public async Task<IActionResult> Index(string? tingkat)
        {
            // === Selalu isi ViewData["Groups"] dengan tipe yang BENAR ===
            var groupRows = await _db.GetKelasGroupsAsync(); // SELECT tingkat, COUNT(*)
            var groupVm = groupRows.Select(g => new kelasGroupVm
            {
                tingkat = g.Tingkat,
                Count = g.Count
            }).ToList();
            ViewData["Groups"] = groupVm;

            if (string.IsNullOrWhiteSpace(tingkat))
            {
                // Halaman grid X / XI / XII
                var vm = new kelasListVm
                {
                    tingkat = null,
                    Items = Enumerable.Empty<kelasModel>()
                };
                return View("~/Views/Adminpage/kelas/Index.cshtml", vm);
            }

            // Halaman daftar kelas per tingkat
            var rows = await _db.GetKelasByGradeAsync(tingkat);
            var items = rows.Select(Map);

            return View(
                "~/Views/Adminpage/kelas/Index.cshtml",
                new kelasListVm { tingkat = tingkat, Items = items }
            );
        }

        // GET /kelas/Create?tingkat=X
        [HttpGet]
        public IActionResult Create(string? tingkat = "X")
        {
            var m = new kelasModel { tingkat = tingkat ?? "X", isactive = true };
            return View("~/Views/Adminpage/kelas/Create.cshtml", m);
        }

        // POST /kelas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(kelasModel m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/kelas/Create.cshtml", m);

            try
            {
                await _db.CreateKelasAsync(m);
                TempData["ok"] = "Kelas berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            // Menangkap error duplikasi dari PostgreSQL (kode 23505)
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                ModelState.AddModelError("", "Kombinasi Tingkat dan Nama Kelas ini sudah digunakan.");
                return View("~/Views/Adminpage/kelas/Create.cshtml", m);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Terjadi kesalahan sistem: " + ex.Message);
                return View("~/Views/Adminpage/kelas/Create.cshtml", m);
            }
        }

        // GET /kelas/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var row = await _db.GetKelasByIdAsync(id);
            if (row is null) return NotFound();

            var m = new kelasModel
            {
                id = row.Id,
                tingkat = row.Tingkat,
                nama = row.Nama,
                isactive = row.IsActive,
                createdat = row.CreatedAt
            };
            return View("~/Views/Adminpage/kelas/Edit.cshtml", m);
        }

        // POST /kelas/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(kelasModel m)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/kelas/Edit.cshtml", m);

            try
            {
                await _db.UpdateKelasAsync(m);
                TempData["ok"] = "Perubahan kelas disimpan.";
                return RedirectToAction(nameof(Index));
            }
            // Menangkap error duplikasi dari PostgreSQL (kode 23505)
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                ModelState.AddModelError("", "Kombinasi Tingkat dan Nama Kelas ini sudah digunakan.");
                return View("~/Views/Adminpage/kelas/Edit.cshtml", m);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Terjadi kesalahan sistem: " + ex.Message);
                return View("~/Views/Adminpage/kelas/Edit.cshtml", m);
            }
        }

        // POST /kelas/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string tingkat)
        {
            try
            {
                await _db.DeleteKelasAsync(id);
                TempData["ok"] = "kelas dihapus.";
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                TempData["err"] = "kelas tidak dapat dihapus karena sedang dipakai data lain.";
            }
            return RedirectToAction(nameof(Index), new { tingkat });
        }

        // GET /kelas/jadwal/5  (id = kelasid)
        [HttpGet]
        public async Task<IActionResult> jadwal(int id)
        {
            var k = await _db.GetKelasByIdAsync(id);
            if (k is null) return NotFound();

            var items = await _db.ListJadwalByKelasAsync(id);

            var vm = new kelasjadwalVm
            {
                kelas = new kelasModel
                {
                    id = k.Id,
                    tingkat = k.Tingkat,
                    nama = k.Nama,
                    isactive = k.IsActive,
                    createdat = k.CreatedAt
                },
                Items = items
            };

            return View("~/Views/Adminpage/kelas/jadwal.cshtml", vm);
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
            sb.AppendLine("hari,Waktu,mapel,guru,ruangan");
            foreach (var r in ordered)
            {
                var hari = DayName(r.Hari);
                var waktu = $"{r.Mulai:hh\\:mm}-{r.Selesai:hh\\:mm}";
                // CSV-safe (quote jika ada koma)
                static string Q(string s) => s?.Contains(',') == true ? $"\"{s.Replace("\"", "\"\"")}\"" : s ?? "";
                sb.AppendLine($"{Q(hari)},{Q(waktu)},{Q(r.Mapel)},{Q(r.GuruNama)},{Q(r.Ruangan ?? "")}");
            }

            var fileName = $"jadwal_{kelas.Tingkat}-{kelas.Nama}.csv";
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

            var title = $"jadwal kelas {kelas.Tingkat}-{kelas.Nama}";
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
                            cols.RelativeColumn(1);   // hari
                            cols.RelativeColumn(1.2f);// Waktu
                            cols.RelativeColumn(1.5f);// mapel
                            cols.RelativeColumn(1.5f);// guru
                            cols.RelativeColumn(1.0f);// ruangan
                        });

                        // header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("hari");
                            header.Cell().Element(CellHeader).Text("Waktu");
                            header.Cell().Element(CellHeader).Text("mapel");
                            header.Cell().Element(CellHeader).Text("guru");
                            header.Cell().Element(CellHeader).Text("ruangan");

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

            var fileName = $"jadwal_{kelas.Tingkat}-{kelas.Nama}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

    }
}
