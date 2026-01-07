using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class MapelController : Controller
    {
        private readonly DapperDb _db;
        public MapelController(DapperDb db) => _db = db;

        // LIST
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var rows = await _db.GetMapelWithCountsAsync();
            return View("~/Views/Adminpage/Mapel/Index.cshtml", rows);
        }

        // CREATE
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new MapelModel
            {
                Kode = await _db.GenerateNextMapelCodeAsync()
            };
            return View("~/Views/Adminpage/Mapel/Create.cshtml", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MapelModel m)
        {
            // Nama wajib, kode kita generate sendiri
            if (string.IsNullOrWhiteSpace(m.Nama))
                ModelState.AddModelError(nameof(m.Nama), "Nama wajib diisi.");

            if (!ModelState.IsValid)
            {
                // kalau validasi gagal, tetap tampilkan kode yang otomatis
                if (string.IsNullOrWhiteSpace(m.Kode))
                    m.Kode = await _db.GenerateNextMapelCodeAsync();

                return View("~/Views/Adminpage/Mapel/Create.cshtml", m);
            }

            try
            {
                // Paksa pakai kode auto generator, abaikan input user
                m.Kode = await _db.GenerateNextMapelCodeAsync();

                await _db.CreateMapelAsync(m);
                TempData["ok"] = "Mapel berhasil ditambahkan.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627) // duplicate
            {
                ModelState.AddModelError("", "Kode/Nama sudah digunakan.");
                // regenerate untuk tampilan ulang, kalau perlu
                if (string.IsNullOrWhiteSpace(m.Kode))
                    m.Kode = await _db.GenerateNextMapelCodeAsync();

                return View("~/Views/Adminpage/Mapel/Create.cshtml", m);
            }
        }

        // EDIT
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var m = await _db.GetMapelByIdAsync(id);
            if (m is null) return NotFound();
            return View("~/Views/Adminpage/Mapel/Edit.cshtml", m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MapelModel m)
        {
            if (string.IsNullOrWhiteSpace(m.Kode))
                ModelState.AddModelError(nameof(m.Kode), "Kode wajib diisi.");
            if (string.IsNullOrWhiteSpace(m.Nama))
                ModelState.AddModelError(nameof(m.Nama), "Nama wajib diisi.");

            if (!ModelState.IsValid)
                return View("~/Views/Adminpage/Mapel/Edit.cshtml", m);

            try
            {
                await _db.UpdateMapelAsync(m);
                TempData["ok"] = "Perubahan disimpan.";
                return RedirectToAction(nameof(Index));
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                ModelState.AddModelError("", "Kode/Nama sudah digunakan.");
                return View("~/Views/Adminpage/Mapel/Edit.cshtml", m);
            }
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _db.DeleteMapelAsync(id);
                TempData["ok"] = "Mapel dihapus.";
            }
            catch (SqlException ex) when (ex.Number == 547) // FK constraint
            {
                TempData["err"] = "Tidak bisa menghapus: mapel masih dipakai oleh data guru.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
