using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Latihan2.Services;
using System.Security.Claims;

namespace Latihan2.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah")]
    // PENTING: Tambahkan Route kosong di class agar controller ini menangani root path
    public class PageGuruController : Controller
    {
        private readonly DapperDb2 _db;
        public PageGuruController(DapperDb2 db) => _db = db;

        public record GuruDashboardVm(DapperDb2.GuruModel Guru, IEnumerable<DapperDb2.JadwalRow> Items);

        // ==========================================
        // MENANGANI HALAMAN UTAMA
        // ==========================================
        // URL: /guru/Pageguru
        [HttpGet("Pageguru")]
        public async Task<IActionResult> Pageguru()
        {
            // 1. AMBIL GURU ID DARI CLAIM
            var guruIdClaim = User.FindFirst("GuruId")?.Value;

            if (string.IsNullOrEmpty(guruIdClaim) || guruIdClaim == "0")
            {
                // Jika login tapi claim hilang, paksa logout agar login ulang
                return RedirectToAction("Logout", "Auth");
            }

            int gid = int.Parse(guruIdClaim);

            // 2. AMBIL DATA
            var guru = await _db.GetGuruByIdAsync(gid);
            if (guru is null)
                return NotFound("Data guru tidak ditemukan (ID Database mungkin berbeda dengan ID Login).");

            var items = await _db.ListJadwalByGuruAsync(gid);

            var vm = new GuruDashboardVm(guru, items);
            // Pastikan path View benar
            return View("~/Views/PageGuru/Pageguru.cshtml", vm);
        }
    }
}