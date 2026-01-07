using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class BuatJadwalController : Controller
    {
        [HttpGet("buat-jadwal")] // URL: /buat-jadwal
        public IActionResult BuatJadwal()
        {
            return View("~/Views/AdminPage/BuatJadwal.cshtml");
        }
    }
}