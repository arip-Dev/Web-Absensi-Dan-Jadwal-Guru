using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class mapelController : Controller
    {
        private readonly DapperDb _db;
        public mapelController(DapperDb db) => _db = db;

        // LIST
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rows = await _db.GetMapelWithCountsAsync();
            return View("~/Views/Adminpage/mapel/Index.cshtml", rows);
        }

        // CREATE
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new mapelModel
            {
                kode = await _db.GenerateNextMapelCodeAsync()
            };
            return View("~/Views/Adminpage/mapel/Create.cshtml", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(mapelModel m)
        {
            // nama wajib, kode kita generate sendiri
            if (string.IsNullOrWhiteSpace(m.nama))
                ModelState.AddModelError(nameof(m.nama), "nama wajib diisi.");

            if (!ModelState.IsValid)
            {
                // kalau validasi gagal, tetap tampilkan kode yang otomatis
                if (string.IsNullOrWhiteSpace(m.kode))
                    m.kode = await _db.GenerateNextMapelCodeAsync();

                return View("~/Views/Adminpage/mapel/Create.cshtml", m);
            }

            try
            {
                // Paksa pakai kode auto generator, abaikan input user
                m.kode = await _db.GenerateNextMapelCodeAsync();

                await _db.CreateMapelAsync(m);
                TempData["ok"] = "mapel berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) // duplicate
            {
                ModelState.AddModelError("", "kode/nama sudah digunakan.");
                // regenerate untuk tampilan ulang, kalau perlu
                if (string.IsNullOrWhiteSpace(m.kode))
                    m.kode = await _db.GenerateNextMapelCodeAsync();

                return View("~/Views/Adminpage/mapel/Create.cshtml", m);
            }
        }

        // EDIT
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.GetMapelByIdAsync(id);
            if (m is null) return NotFound();
            return View("~/Views/Adminpage/mapel/Edit.cshtml", m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(mapelModel m)
        {
            if (string.IsNullOrWhiteSpace(m.kode))
                ModelState.AddModelError(nameof(m.kode), "kode wajib diisi.");
            if (string.IsNullOrWhiteSpace(m.nama))
                ModelState.AddModelError(nameof(m.nama), "nama wajib diisi.");

            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/mapel/Edit.cshtml", m);

            try
            {
                await _db.UpdateMapelAsync(m);
                TempData["ok"] = "Perubahan disimpan.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                ModelState.AddModelError("", "kode/nama sudah digunakan.");
                return View("~/Views/Adminpage/mapel/Edit.cshtml", m);
            }
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Mencoba menghapus data mapel
                await _db.DeleteMapelAsync(id);

                TempData["ok"] = "Mata pelajaran berhasil dihapus.";
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                // 23503 adalah kode PostgreSQL untuk pelanggaran Foreign Key (Data masih dipakai)
                TempData["Err"] = "Gagal menghapus: Mata pelajaran ini tidak bisa dihapus karena masih digunakan oleh satu atau lebih Guru.";
            }
            catch (Exception ex)
            {
                // Menangkap error lainnya
                TempData["Err"] = "Terjadi kesalahan sistem: " + ex.Message;
            }

            // Sesuaikan "Index" dengan nama method halaman list mapel kamu (misalnya Mapel_data)
            return RedirectToAction(nameof(Index));
        }
    }
}
