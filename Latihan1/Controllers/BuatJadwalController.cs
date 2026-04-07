using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class BuatjadwalController : Controller
    {
        [HttpGet("buat-jadwal")] // URL: /buat-jadwal
        public IActionResult Buatjadwal()
        {
            return View("~/Views/AdminPage/Buatjadwal.cshtml");
        }
    }
}