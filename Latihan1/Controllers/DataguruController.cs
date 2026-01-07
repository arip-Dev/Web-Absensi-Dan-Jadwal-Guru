using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QRCoder; // Wajib ada untuk generate QR
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class DataguruController : Controller
    {
        private readonly DapperDb _db;
        private readonly IPasswordHasher<string> _hasher;

        public DataguruController(DapperDb db, IPasswordHasher<string> hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        // ================= HELPER: Generate QR Code =================
        private string? GenerateQrCode(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    // Membuat data QR Code
                    QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);

                    // Render ke format PNG Byte (Ringan & Cross-platform)
                    PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                    byte[] qrCodeBytes = qrCode.GetGraphic(20);

                    // Kembalikan sebagai string Base64 agar bisa disimpan di DB / ditampilkan di HTML
                    return "data:image/png;base64," + Convert.ToBase64String(qrCodeBytes);
                }
            }
            catch
            {
                return null;
            }
        }

        // ================= LIST =================
        [HttpGet]
        public async Task<IActionResult> Teacher_data(string? q, int page = 1, int pageSize = 10)
        {
            var allowed = new[] { 10, 25, 50, 100 };
            if (!allowed.Contains(pageSize)) pageSize = 10;

            var (items, total) = await _db.SearchGuruPagedAsync(q, page, pageSize);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var vm = new TeacherListViewModel
            {
                Items = items,
                Query = q,
                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = totalPages
            };

            return View("~/Views/Adminpage/Dataguru/Dataguru.cshtml", vm);
        }

        // ================= CREATE (GET) =================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var mapels = await _db.GetAllMapelAsync();
            ViewBag.MapelList = new SelectList(mapels, "Id", "Nama");

            var m = new GuruModel
            {
                IsActive = true,
                MaxWeeklyLoad = 24,
                MaxDailyLoad = 6,
                MaxConsecutiveSlots = 3,
                MapelIds = Array.Empty<int>()
            };

            return View("~/Views/Adminpage/Dataguru/Create.cshtml", m);
        }

        // ================= CREATE (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GuruModel m)
        {
            var selectedMapelIds = m.MapelIds ?? Array.Empty<int>();

            // --- VALIDASI NIP UNIK ---
            if (!string.IsNullOrWhiteSpace(m.NIP))
            {
                bool nipExists = await _db.IsNipExistsAsync(m.NIP);
                if (nipExists)
                {
                    ModelState.AddModelError("NIP", "NIP ini sudah digunakan oleh guru lain.");
                }
            }

            // Validasi Mapel
            if (selectedMapelIds.Length == 0)
            {
                ModelState.AddModelError("MapelIds", "Minimal pilih satu mata pelajaran.");
            }
            // 2. LOGIKA BARU: Validasi Email Unik agar tidak crash ke DB
            if (!string.IsNullOrWhiteSpace(m.Email))
            {
                // Pastikan Anda sudah membuat method IsEmailExistsAsync di DapperDb
                bool isExist = await _db.IsEmailExistsAsync(m.Email);
                if (isExist)
                {
                    // Pesan ini yang akan muncul sebagai alert di bawah input email
                    ModelState.AddModelError("Email", "Email ini sudah digunakan oleh guru lain.");
                }
            }

            if (!ModelState.IsValid)
            {
                var mapels = await _db.GetAllMapelAsync();
                ViewBag.MapelList = new SelectList(mapels, "Id", "Nama", selectedMapelIds);
                return View("~/Views/Adminpage/Dataguru/Create.cshtml", m);
            }

            // Mapel utama (untuk backward compatibility kolom MapelId)
            m.MapelId = selectedMapelIds[0];

            // --- GENERATE QR CODE ---
            // Kita generate QR berdasarkan NIP. Pastikan NIP terisi.
            if (!string.IsNullOrEmpty(m.NIP))
            {
                m.QRCodeBase64 = GenerateQrCode(m.NIP);
            }

            // 1) Simpan guru
            // NOTE: Pastikan DapperDb.CreateGuruAsync sudah dimodifikasi untuk menyimpan m.QRCodeBase64!
            var guruId = await _db.CreateGuruAsync(m);

            // 2) Simpan relasi ke tabel GuruMapel
            await _db.SaveGuruMapelAsync(guruId, selectedMapelIds);

            // 3) Siapkan akun user (jika ada email)
            var username = m.Email?.Trim();
            if (!string.IsNullOrWhiteSpace(username))
            {
                var existingUserId = await _db.GetUserIdByUsernameAsync(username);

                if (existingUserId is null)
                {
                    const string defaultPassword = "guru123";
                    var hash = _hasher.HashPassword(username!, defaultPassword);
                    await _db.CreateUserAsync(username!, hash, role: "Guru", guruId: guruId);
                }
                else if (existingUserId is int userId)
                {
                    await _db.UpdateUserGuruIdAsync(userId, guruId);
                }
            }

            TempData["ok"] = $"Guru '{m.Nama}' berhasil ditambahkan (QR Code Generated).";
            return RedirectToAction(nameof(Teacher_data));
        }

        // ================= EDIT (GET) =================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            // Method ini memanggil GetGuruByIdAsync yang sudah kita update 
            // untuk menghitung JamMengajar dari subquery tabel Jadwal.
            var m = await _db.GetGuruForEditAsync(id);
            if (m is null) return NotFound();

            var mapelIdsEnum = await _db.GetMapelIdsForGuruAsync(id);
            var mapelIds = mapelIdsEnum ?? Enumerable.Empty<int>();
            m.MapelIds = mapelIds.ToArray();

            var locked = (await _db.GetLockedMapelIdsForGuruAsync(id)).ToArray();
            ViewBag.LockedMapelIds = locked;

            var mapels = await _db.GetAllMapelAsync();
            // Menggunakan SelectList untuk keperluan dropdown (jika masih diperlukan)
            ViewBag.MapelList = new SelectList(mapels, "Id", "Nama");

            return View("~/Views/Adminpage/Dataguru/Edit.cshtml", m);
        }

        // ================= EDIT (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(GuruModel m)
        {
            var selectedMapelIds = m.MapelIds ?? Array.Empty<int>();
            var locked = (await _db.GetLockedMapelIdsForGuruAsync(m.Id)).ToArray();
            var removedLocked = locked.Except(selectedMapelIds).ToArray();

            // --- VALIDASI NIP UNIK ---
            if (!string.IsNullOrWhiteSpace(m.NIP))
            {
                bool nipExists = await _db.IsNipExistsAsync(m.NIP, m.Id);
                if (nipExists)
                {
                    ModelState.AddModelError("NIP", "NIP sudah terdaftar pada guru lain.");
                }
            }

            // Validasi Email Unik (Kecuali untuk ID dirinya sendiri)
            if (!string.IsNullOrWhiteSpace(m.Email))
            {
                bool emailExists = await _db.IsEmailExistsAsync(m.Email, m.Id);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Email ini sudah digunakan oleh guru lain.");
                }
            }

            if (removedLocked.Any())
            {
                ModelState.AddModelError("MapelIds", "Tidak boleh menghapus mata pelajaran yang sudah dipakai di jadwal.");
            }
            if (selectedMapelIds.Length == 0)
            {
                ModelState.AddModelError("MapelIds", "Minimal pilih satu mata pelajaran.");
            }

            if (!ModelState.IsValid)
            {
                var mapels = await _db.GetAllMapelAsync();
                ViewBag.MapelList = new SelectList(mapels, "Id", "Nama", selectedMapelIds);
                ViewBag.LockedMapelIds = locked;
                return View("~/Views/Adminpage/Dataguru/Edit.cshtml", m);
            }

            selectedMapelIds = locked.Union(selectedMapelIds).Distinct().ToArray();
            m.MapelIds = selectedMapelIds;
            m.MapelId = selectedMapelIds[0];

            // --- RE-GENERATE QR CODE (Jika NIP berubah atau QR kosong) ---
            if (!string.IsNullOrEmpty(m.NIP))
            {
                // Generate ulang untuk memastikan QR sesuai dengan NIP terbaru
                m.QRCodeBase64 = GenerateQrCode(m.NIP);
            }

            // NOTE: Pastikan DapperDb.UpdateGuruAsync sudah dimodifikasi untuk menyimpan m.QRCodeBase64!
            await _db.UpdateGuruAsync(m);

            await _db.DeleteGuruMapelAsync(m.Id);
            await _db.SaveGuruMapelAsync(m.Id, selectedMapelIds);

            TempData["ok"] = "Perubahan data guru disimpan.";
            return RedirectToAction(nameof(Teacher_data));
        }

        // ================= DELETE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, bool deleteUser = true)
        {
            var guru = await _db.GetGuruByIdAsync(id);
            if (guru is null)
            {
                TempData["Err"] = "Guru tidak ditemukan.";
                return RedirectToAction(nameof(Teacher_data));
            }

            // 1. Cek Jadwal (Logic Bisnis: Guru yg punya jadwal tidak boleh dihapus)
            if (await _db.GuruHasJadwalAsync(id))
            {
                TempData["ErrGuruSchedule"] = $"Guru '{guru.Nama}' masih memiliki jadwal mengajar. Hapus jadwal terlebih dahulu.";
                return RedirectToAction(nameof(Teacher_data));
            }

            // 2. Hapus Data (Dibungkus Try-Catch untuk menangkap FK Constraint Absensi)
            try
            {
                // CATATAN: Tidak perlu panggil DeleteGuruMapelAsync terpisah lagi,
                // karena sudah satu paket di dalam DeleteGuruAndDetachUsersAsync.
                await _db.DeleteGuruAndDetachUsersAsync(id, deleteUser);

                TempData["ok"] = "Data guru berhasil dihapus.";
            }
            catch (Microsoft.Data.SqlClient.SqlException ex)
            {
                // Error Number 547 = Conflict Foreign Key Constraint (Data dipakai di tabel lain)
                if (ex.Number == 547)
                {
                    TempData["Err"] = $"Gagal menghapus: Guru '{guru.Nama}' tidak bisa dihapus karena datanya masih tercatat di Absensi. Silakan non-aktifkan guru ini jika data historis ingin dipertahankan.";
                }
                else
                {
                    TempData["Err"] = "Terjadi kesalahan database: " + ex.Message;
                }
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Terjadi kesalahan sistem: " + ex.Message;
            }

            return RedirectToAction(nameof(Teacher_data));
        }
    }
}