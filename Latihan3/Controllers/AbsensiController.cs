using Latihan3.Models;
using Latihan3.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Latihan3.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    [Route("Manage")]
    public class AbsensiController : Controller
    {
        private readonly DapperDb3 _db;

        public AbsensiController(DapperDb3 db)
        {
            _db = db;
        }

        [HttpGet("Index")]
        public async Task<IActionResult> Index(int page = 1)
        {
            int pageSize = 10; // Default 10 data saja
            var data = await _db.GetPagedAbsensiAsync(page, pageSize);

            // Kirim informasi halaman ke View
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)await _db.GetTotalAbsensiCountAsync() / pageSize);

            return View(data);
        }

        // ================= FITUR SCAN (HADIR) =================
        [HttpGet("Scan")]
        public IActionResult Scan()
        {
            return View();
        }

        [HttpPost("SubmitScan")]
        public async Task<IActionResult> SubmitScan(string nip)
        {
            if (string.IsNullOrWhiteSpace(nip))
            {
                return Json(new { success = false, message = "QR Code tidak valid." });
            }

            // 1. Cari Guru berdasarkan NIP
            var guruId = await _db.GetGuruIdByNipAsync(nip);
            if (guruId == null)
            {
                return Json(new { success = false, message = "Guru tidak ditemukan (NIP tidak terdaftar)." });
            }

            // --- PERBAIKAN PENTING ---
            // Gunakan satu variabel waktu acuan untuk Logic DAN Insert
            var waktuSekarang = DateTime.UtcNow.AddHours(7);

            // 2. Cek apakah sudah absen hari ini
            // JANGAN PAKAI DateTime.Now, tapi pakai 'waktuSekarang' yang sudah di-set ke WIB
            var isAlready = await _db.IsAlreadyPresentAsync(guruId.Value, waktuSekarang);

            if (isAlready)
            {
                var nama = await _db.GetGuruNamaByIdAsync(guruId.Value);
                return Json(new { success = false, message = $"Guru '{nama}' sudah melakukan absensi hari ini." });
            }

            // 3. Simpan Absensi (Otomatis Hadir)
            var abs = new AbsensiGuru
            {
                Id = guruId.Value,
                Tanggal = waktuSekarang, // Konsisten menggunakan waktuSekarang
                Status = "Hadir",
                Keterangan = "Scan QR Code"
            };

            await _db.InsertAbsensiAsync(abs);
            var namaSukses = await _db.GetGuruNamaByIdAsync(guruId.Value);

            return Json(new { success = true, message = $"Selamat datang, {namaSukses}!", nama = namaSukses });
        }

        // ================= FITUR MANUAL (SAKIT/IZIN/CREATE) =================
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            // --- PERBAIKAN DISINI ---
            // JANGAN panggil _db.GetGuruListAsync()
            // Set ViewBag menjadi NULL agar HTML <select> kosong saat awal load.
            ViewBag.GuruList = null;

            // Kode setup waktu tetap sama
            var waktuSekarangWIB = DateTime.UtcNow.AddHours(7);
            var model = new AbsensiGuru
            {
                Tanggal = waktuSekarangWIB,
                Status = "Hadir"
            };

            return View(model);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AbsensiGuru abs)
        {
            ModelState.Remove("NamaGuru");
            ModelState.Remove("NIP");
            ModelState.Remove("AbsensiId");

            if (!ModelState.IsValid)
            {
                // JIKA ERROR: Kita harus me-load data guru yang SUDAH DIPILIH saja
                // Jangan load semua ribuan guru lagi.
                if (abs.Id != 0)
                {
                    var existingGuru = await _db.GetGuruByIdAsync(abs.Id);
                    if (existingGuru != null)
                    {
                        // Buat list isi 1 item saja untuk mengisi dropdown kembali
                        var singleList = new List<object> { new { Id = existingGuru.Id, Nama = existingGuru.Nama } };
                        ViewBag.GuruList = new SelectList(singleList, "Id", "Nama", abs.Id);
                    }
                }

                return View(abs);
            }

            // Cek duplikasi absensi
            var isAlready = await _db.IsAlreadyPresentAsync(abs.Id, abs.Tanggal);
            if (isAlready)
            {
                ModelState.AddModelError("", "Guru ini sudah absen.");

                // Load ulang data guru yang dipilih saja (sama seperti di atas)
                var existingGuru = await _db.GetGuruByIdAsync(abs.Id);
                if (existingGuru != null)
                {
                    var singleList = new List<object> { new { Id = existingGuru.Id, Nama = existingGuru.Nama } };
                    ViewBag.GuruList = new SelectList(singleList, "Id", "Nama", abs.Id);
                }
                return View(abs);
            }

            await _db.InsertAbsensiAsync(abs);
            // 2. Ambil Nama Guru (Opsional: Agar pesan lebih spesifik)
            var guru = await _db.GetGuruByIdAsync(abs.Id);
            string namaGuru = guru?.Nama ?? "Guru";

            // 3. Simpan Pesan Sukses ke TempData
            // TempData akan hilang otomatis setelah dibaca satu kali
            TempData["ok"] = $"Berhasil! Data absensi untuk '{namaGuru}' telah disimpan.";

            // 4. Redirect ke Index
            return RedirectToAction(nameof(Index));
        }

        // ================= EDIT =================
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var abs = await _db.GetAbsensiByIdAsync(id);
            if (abs == null) return NotFound();

            var gurus = await _db.GetGuruListAsync();
            ViewBag.GuruList = new SelectList(gurus, "Id", "Nama", abs.Id);

            return View(abs);
        }

        // Method Edit (POST)
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AbsensiGuru abs)
        {
            // --- LANGKAH 1 & 2 (AMBIL DATA LAMA & PERBAIKI DATA) ---
            var dataLama = await _db.GetAbsensiByIdAsync(id);
            if (dataLama == null) return NotFound();

            abs.AbsensiId = id;
            abs.Id = dataLama.Id; // Pastikan Guru ID tidak berubah/hilang

            ModelState.Remove("NamaGuru");
            ModelState.Remove("NIP");

            // --- LANGKAH 3: VALIDASI ---
            if (!ModelState.IsValid)
            {
                return View(abs);
            }

            // --- LANGKAH 4: SIMPAN UPDATE ---
            try
            {
                await _db.UpdateAbsensiAsync(abs);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "DATABASE ERROR: " + ex.Message);
                return View(abs);
            }

            // --- PERBAIKAN: TAMBAHKAN PESAN SUKSES ---
            // Ambil nama guru lagi untuk pesan notifikasi
            var namaGuru = await _db.GetGuruNamaByIdAsync(abs.Id);
            string namaUntukPesan = namaGuru ?? "Guru";

            TempData["ok"] = $"Data absensi milik '{namaUntukPesan}' berhasil diperbarui.";
            // ------------------------------------------

            return RedirectToAction(nameof(Index));
        }

        // ================= DELETE =================
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var abs = await _db.GetAbsensiByIdAsync(id);
            if (abs == null) return NotFound();
            return View(abs);
        }

        [HttpPost("Delete/{id}")]
        [ActionName("Delete")] // Agar klop dengan asp-action="Delete" di View
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // 1. Ambil detail data dulu SEBELUM dihapus (untuk mendapatkan nama)
            var abs = await _db.GetAbsensiByIdAsync(id);

            // Jika data sudah tidak ada, langsung balik saja
            if (abs == null)
            {
                return RedirectToAction(nameof(Index));
            }

            // 2. Simpan nama gurunya ke variabel
            // (Perhatikan: abs.NamaGuru sudah tersedia karena query GetAbsensiByIdAsync melakukan JOIN)
            string namaGuru = abs.NamaGuru ?? "Guru";

            // 3. Hapus Data
            await _db.DeleteAbsensiAsync(id);

            // 4. Buat Pesan Sukses
            TempData["ok"] = $"Data absensi milik '{namaGuru}' telah berhasil dihapus.";

            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Rekapitulasi(int? bulan, int? tahun, int page = 1, string? search = null)
        {
            int pageSize = 10;
            int sBulan = bulan ?? DateTime.Now.Month;
            int sTahun = tahun ?? DateTime.Now.Year;

            // Hitung total data berdasarkan kata kunci pencarian
            int totalGuruMatch = await _db.GetTotalGuruCountAsync(search);

            ViewBag.SelectedBulan = sBulan;
            ViewBag.SelectedTahun = sTahun;
            ViewBag.CurrentPage = page;
            ViewBag.SearchTerm = search;
            ViewBag.TotalMatch = totalGuruMatch; // Untuk teks "Menampilkan X data"
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalGuruMatch / pageSize);

            var rekap = await _db.GetPagedRekapAbsensiAsync(sBulan, sTahun, page, pageSize, search);
            return View(rekap);
        }
        // ================= ENDPOINT BARU UNTUK SELECT2 AJAX =================
        [HttpGet("SearchGuru")]
        public async Task<IActionResult> SearchGuru(string term) // 'term' adalah parameter default Select2
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return Json(new { results = new object[] { } });
            }

            // Panggil query search yang baru dibuat
            var data = await _db.SearchGuruByNameAsync(term);

            // Format JSON harus sesuai standar Select2: { id: ..., text: ... }
            var result = data.Select(g => new
            {
                id = g.Id,
                text = $"{g.Nama} - {g.NIP}" // Bisa digabung agar informatif
            });

            return Json(new { results = result });
        }
    }
}