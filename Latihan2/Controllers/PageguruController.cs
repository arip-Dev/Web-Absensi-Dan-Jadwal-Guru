using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Latihan2.Services;
using System.Security.Claims;

namespace Latihan2.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah")]
    // PENTING: Tambahkan Route kosong di class agar controller ini menangani root path
    public class PageguruController : Controller
    {
        private readonly DapperDb2 _db;
        public PageguruController(DapperDb2 db) => _db = db;

        public record guruDashboardVm(DapperDb2.GuruModel guru, IEnumerable<DapperDb2.JadwalRow> Items);

        // ==========================================
        // MENANGANI HALAMAN UTAMA
        // ==========================================
        // URL: /guru/Pageguru
        [HttpGet("Pageguru")]
        public async Task<IActionResult> Pageguru()
        {
            // 1. AMBIL guru id DARI CLAIM
            var guruidClaim = User.FindFirst("guruid")?.Value;

            if (string.IsNullOrEmpty(guruidClaim) || guruidClaim == "0")
            {
                // Jika login tapi claim hilang, paksa logout agar login ulang
                return RedirectToAction("Logout", "Auth");
            }

            int gid = int.Parse(guruidClaim);

            // 2. AMBIL DATA
            var guru = await _db.GetGuruByIdAsync(gid);
            if (guru is null)
                return NotFound("Data guru tidak ditemukan (id Database mungkin berbeda dengan id Login).");

            var items = await _db.ListJadwalByGuruAsync(gid);

            var vm = new guruDashboardVm(guru, items);
            // Pastikan path View benar
            return View("~/Views/Pageguru/Pageguru.cshtml", vm);
        }
    }
}