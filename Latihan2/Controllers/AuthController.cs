using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Latihan2.Services;
using Microsoft.AspNetCore.Identity; // Butuh package Microsoft.Extensions.Identity.Core
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

namespace Latihan2.Controllers
{
    [Route("Auth")]
    public class AuthController : Controller
    {
        private readonly DapperDb2 _db;
        private readonly IPasswordHasher<string> _hasher; // Untuk cek password
        private readonly IConfiguration _config;

        // 2. Tambahkan IConfiguration ke dalam Constructor
        public AuthController(DapperDb2 db, IPasswordHasher<string> hasher, IConfiguration config)
        {
            _db = db;
            _hasher = hasher;
            _config = config; // 3. Isi field dengan parameter config
        }

        // 1. TAMPILKAN HALAMAN LOGIN (Milik Sendiri)
        [HttpGet("~/")]
        [HttpGet("Login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Pageguru", "Pageguru");
            }

            // Jika user masuk dengan returnUrl=/guru atau /, kita bersihkan URL-nya
            if (returnUrl == "/" || returnUrl == "/guru")
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        // 2. PROSES LOGIN (Cek Database Langsung)
        [HttpPost("Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] // Keamanan standar
        public async Task<IActionResult> Login(string nip, string password)
        {
            // 1. Ambil Hash Password dari Database berdasarkan username (nip)
            var storedHash = await _db.GetPasswordHashAsync(nip);

            if (string.IsNullOrEmpty(storedHash))
            {
                ViewBag.Error = "User tidak ditemukan.";
                return View();
            }

            // 2. Verifikasi Password (Input vs Hash Database)
            var verificationResult = _hasher.VerifyHashedPassword(nip, storedHash, password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Password salah.";
                return View();
            }

            // 3. Ambil Data guru Lengkap (setelah password valid)
            // Kita pakai LoginguruDirectAsync karena password sudah diverifikasi di atas
            var guru = await _db.LoginGuruDirectAsync(nip);

            if (guru == null)
            {
                ViewBag.Error = "Data guru tidak ditemukan (Mungkin akun admin, bukan guru).";
                return View();
            }

            // 4. Buat Tiket Masuk (Cookie)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, guru.Nama),
                new Claim("guruid", guru.Id.ToString()), // PENTING: id ini dipakai PageguruController
                new Claim(ClaimTypes.Role, "guru")
            };

            var Identity = new ClaimsIdentity(claims, "CookieSekolah");
            var principal = new ClaimsPrincipal(Identity);

            // Simpan Cookie (Ingat Saya = 7 hari)
            await HttpContext.SignInAsync("CookieSekolah", principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTime.UtcNow.AddDays(7)
            });

            return RedirectToAction("Pageguru", "Pageguru");
        }

        // 3. LOGOUT (Bersihkan Cookie Sendiri)
        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // 1. Hapus Sesi Latihan2
                await HttpContext.SignOutAsync("CookieSekolah");
                HttpContext.Session.Clear();

                // 2. Hapus Cookie Fisik Latihan2
                var cOptions = new CookieOptions { Path = "/", Expires = DateTimeOffset.UtcNow.AddDays(-1) };
                Response.Cookies.Delete("CookieSekolah", cOptions);

                // 3. PANGGIL LOGOUT LATIHAN1 DENGAN SOURCE
                var latihan1Url = _config["Latihan1:BaseUrl"]?.TrimEnd('/') ?? "";

                // PENTING: Tambahkan ?source=guru agar Latihan1 tahu user ingin keluar total
                return Redirect($"{latihan1Url}/Auth/Logout?source=guru");
            }
            catch
            {
                return RedirectToAction("Login");
            }
        }

        [HttpGet("Denied")]
        [AllowAnonymous]
        public IActionResult Denied() => Content("Akses Ditolak.");

        [HttpGet("SsoLogin")]
        [AllowAnonymous]
        public async Task<IActionResult> SsoLogin(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // Ambil data dari claims JWT
                var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "username")?.Value;
                var role = jwtToken.Claims.FirstOrDefault(c => c.Type == "role")?.Value;
                var guruid = jwtToken.Claims.FirstOrDefault(c => c.Type == "guruid")?.Value;

                if (username == null || role != "guru") return RedirectToAction("Login");

                // Buat Sesi Login untuk Latihan2
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("guruid", guruid ?? "0"),
            new Claim(ClaimTypes.Role, role)
        };

                var Identity = new ClaimsIdentity(claims, "CookieSekolah");
                var principal = new ClaimsPrincipal(Identity);

                await HttpContext.SignInAsync("CookieSekolah", principal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTime.UtcNow.AddDays(7)
                });

                // Redirect ke dashboard guru di Latihan2
                return RedirectToAction("Pageguru", "Pageguru");
            }
            catch (Exception ex)
            {
                // Jika token invalid atau expired
                TempData["Error"] = "Sesi SSO Gagal: " + ex.Message;
                return RedirectToAction("Login");
            }
        }
    }
}