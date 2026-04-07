using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Npgsql; // Wajib untuk menangkap exception database PostgreSQL
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
            // FIX 1: Pastikan mapels tidak null (gunakan ?? empty list)
            var mapels = await _db.GetAllMapelAsync() ?? new List<mapelModel>();

            // Di sini selectedmapelids belum ada, jadi kita buat array kosong
            var selectedmapelids = Array.Empty<int>();

            // Masukkan ke ViewBag
            ViewBag.mapelList = new SelectList(mapels, "id", "nama", selectedmapelids);

            var m = new guruModel
            {
                isactive = true,
                maxweeklyload = 24,
                maxdailyload = 6,
                maxconsecutiveslots = 3,
                mapelids = selectedmapelids // Inisialisasi model dengan array kosong
            };

            return View("~/Views/Adminpage/Dataguru/Create.cshtml", m);
        }

        // ================= CREATE (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(guruModel m)
        {
            var selectedmapelids = m.mapelids ?? Array.Empty<int>();

            // --- VALIDASI nip UNIK ---
            if (!string.IsNullOrWhiteSpace(m.nip))
            {
                bool nipExists = await _db.IsnipExistsAsync(m.nip);
                if (nipExists)
                {
                    ModelState.AddModelError("nip", "nip ini sudah digunakan oleh guru lain.");
                }
            }

            // Validasi mapel
            if (selectedmapelids.Length == 0)
            {
                ModelState.AddModelError("mapelids", "Minimal pilih satu mata pelajaran.");
            }

            // LOGIKA BARU: Validasi email Unik agar tidak crash ke DB
            if (!string.IsNullOrWhiteSpace(m.email))
            {
                bool isExist = await _db.IsEmailExistsAsync(m.email);
                if (isExist)
                {
                    // Pesan ini yang akan muncul sebagai alert di bawah input email
                    ModelState.AddModelError("email", "email ini sudah digunakan oleh guru lain.");
                }
            }

            if (!ModelState.IsValid)
            {
                var mapels = await _db.GetAllMapelAsync();
                ViewBag.mapelList = new SelectList(mapels, "id", "nama", selectedmapelids);
                return View("~/Views/Adminpage/Dataguru/Create.cshtml", m);
            }

            // mapel utama (untuk backward compatibility kolom mapelid)
            m.mapelid = selectedmapelids[0];

            // --- GENERATE QR CODE ---
            // Kita generate QR berdasarkan nip. Pastikan nip terisi.
            if (!string.IsNullOrEmpty(m.nip))
            {
                m.qrcodebase64 = GenerateQrCode(m.nip);
            }

            // 1) Simpan guru
            var guruid = await _db.CreateGuruAsync(m);

            // 2) Simpan relasi ke tabel gurumapel
            await _db.SaveGuruMapelAsync(guruid, selectedmapelids);

            // 3) Siapkan akun user (jika ada email)
            var username = m.email?.Trim();
            if (!string.IsNullOrWhiteSpace(username))
            {
                var existingUserid = await _db.GetUserIdByUsernameAsync(username);

                if (existingUserid is null)
                {
                    const string defaultPassword = "guru123";
                    var hash = _hasher.HashPassword(username!, defaultPassword);
                    await _db.CreateUserAsync(username!, hash, role: "Guru", guruId: guruid);
                }
                else if (existingUserid is int userid)
                {
                    await _db.UpdateUserGuruIdAsync(userid, guruid);
                }
            }

            TempData["ok"] = $"Guru '{m.nama}' berhasil ditambahkan (QR Code Generated).";
            return RedirectToAction(nameof(Teacher_data));
        }

        // ================= EDIT (GET) =================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.GetGuruForEditAsync(id);
            if (m is null) return NotFound();

            var mapelidsEnum = await _db.GetMapelIdsForGuruAsync(id);
            var mapelids = mapelidsEnum ?? Enumerable.Empty<int>();
            m.mapelids = mapelids.ToArray();

            var locked = (await _db.GetLockedMapelIdsForGuruAsync(id)).ToArray();
            ViewBag.Lockedmapelids = locked;

            var mapels = await _db.GetAllMapelAsync();
            ViewBag.mapelList = new SelectList(mapels, "id", "nama");

            return View("~/Views/Adminpage/Dataguru/Edit.cshtml", m);
        }

        // ================= EDIT (POST) =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(guruModel m)
        {
            var selectedmapelids = m.mapelids ?? Array.Empty<int>();
            var locked = (await _db.GetLockedMapelIdsForGuruAsync(m.id)).ToArray();
            var removedLocked = locked.Except(selectedmapelids).ToArray();

            // --- VALIDASI nip UNIK ---
            if (!string.IsNullOrWhiteSpace(m.nip))
            {
                bool nipExists = await _db.IsnipExistsAsync(m.nip, m.id);
                if (nipExists)
                {
                    ModelState.AddModelError("nip", "nip sudah terdaftar pada guru lain.");
                }
            }

            // Validasi email Unik (Kecuali untuk id dirinya sendiri)
            if (!string.IsNullOrWhiteSpace(m.email))
            {
                bool emailExists = await _db.IsEmailExistsAsync(m.email, m.id);
                if (emailExists)
                {
                    ModelState.AddModelError("email", "email ini sudah digunakan oleh guru lain.");
                }
            }

            if (removedLocked.Any())
            {
                ModelState.AddModelError("mapelids", "Tidak boleh menghapus mata pelajaran yang sudah dipakai di jadwal.");
            }
            if (selectedmapelids.Length == 0)
            {
                ModelState.AddModelError("mapelids", "Minimal pilih satu mata pelajaran.");
            }

            if (!ModelState.IsValid)
            {
                var mapels = await _db.GetAllMapelAsync();
                ViewBag.mapelList = new SelectList(mapels, "id", "nama", selectedmapelids);
                ViewBag.Lockedmapelids = locked;
                return View("~/Views/Adminpage/Dataguru/Edit.cshtml", m);
            }

            selectedmapelids = locked.Union(selectedmapelids).Distinct().ToArray();
            m.mapelids = selectedmapelids;
            m.mapelid = selectedmapelids[0];

            // --- RE-GENERATE QR CODE (Jika nip berubah atau QR kosong) ---
            if (!string.IsNullOrEmpty(m.nip))
            {
                m.qrcodebase64 = GenerateQrCode(m.nip);
            }

            await _db.UpdateGuruAsync(m);

            await _db.DeleteGuruMapelAsync(m.id);
            await _db.SaveGuruMapelAsync(m.id, selectedmapelids);

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

            // 1. Cek jadwal (Logic Bisnis: guru yg punya jadwal tidak boleh dihapus)
            if (await _db.GuruHasJadwalAsync(id))
            {
                TempData["ErrguruSchedule"] = $"Guru '{guru.nama}' masih memiliki jadwal mengajar. Hapus jadwal terlebih dahulu.";
                return RedirectToAction(nameof(Teacher_data));
            }

            // 2. Hapus Data (Dibungkus Try-Catch untuk menangkap FK Constraint Absensi PostgreSQL)
            try
            {
                await _db.DeleteGuruAndDetachUsersAsync(id, deleteUser);
                TempData["ok"] = "Data guru berhasil dihapus.";
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                // SqlState 23503 = Foreign Key Constraint Violation
                TempData["Err"] = $"Gagal menghapus: Guru '{guru.nama}' tidak bisa dihapus karena datanya masih tercatat di Absensi atau Relasi lain. Silakan non-aktifkan guru ini jika data historis ingin dipertahankan.";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Terjadi kesalahan sistem: " + ex.Message;
            }

            return RedirectToAction(nameof(Teacher_data));
        }
    }
}