using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Latihan1.Controllers
{
    [AllowAnonymous]
    public class AuthController : Controller
    {
        private readonly DapperDb _db;
        private readonly IPasswordHasher<string> _hasher;
        private readonly IConfiguration _config;

        public AuthController(DapperDb db, IPasswordHasher<string> hasher, IConfiguration config)
        {
            _db = db;
            _hasher = hasher;
            _config = config;
        }

        // URL: /Auth/Login
        [HttpGet("Auth/Login")]
        public IActionResult Login(string? returnUrl = null, string? from = null)
        {
            // --- 1. DETEKSI USER SUDAH LOGIN TAPI COBA TEMBAK URL ADMIN (guru) ---
            if (User.Identity?.IsAuthenticated == true)
            {
                // Jika dia guru tapi mencoba mengakses URL yang mengandung kata "Admin"
                if (User.IsInRole("guru") && !string.IsNullOrEmpty(returnUrl) &&
                    returnUrl.Contains("/Admin", StringComparison.OrdinalIgnoreCase))
                {
                    ViewBag.Error = "Akses Ditolak: Akun Anda (guru) tidak diizinkan mengakses halaman Admin.";
                    return View();
                }

                // Jika dia Admin atau sedang proses normal, arahkan sesuai logika Anda yang lama
                string username = User.Identity?.Name ?? "Unknown";
                string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "guru";
                string guruid = User.FindFirst("guruid")?.Value ?? "0";

                if (from == "absensi") return RedirectToAbsensiWithToken(username, role, guruid);
                if (User.IsInRole("Admin")) return RedirectToAction("Admin_page", "Admin");
                return RedirectToLatihan2WithToken(username, role, guruid);
            }

            // --- 2. DETEKSI USER BELUM LOGIN COBA TEMBAK URL ---
            if (!string.IsNullOrEmpty(returnUrl))
            {
                ViewBag.Error = "Akses Ditolak: Anda perlu login terlebih dahulu untuk mengakses halaman Admin.";
            }

            ViewData["ReturnUrl"] = returnUrl;
            ViewData["FromApp"] = from;

            return View();
        }

        // URL: /Auth/Login (Proses POST)
        [HttpPost("Auth/Login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null, string? from = null)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _db.GetUserByUsernameAsync(vm.username);
            if (user is null || _hasher.VerifyHashedPassword(user.Username, user.PasswordHash, vm.Password) == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "username atau password salah.";
                return View(vm);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };
            if (user.GuruId is not null) claims.Add(new Claim("guruid", user.GuruId.Value.ToString()));

            var Identity = new ClaimsIdentity(claims, "CookieSekolah");
            await HttpContext.SignInAsync("CookieSekolah", new ClaimsPrincipal(Identity));

            // REDIRECT LOGIC
            if (from == "absensi") return RedirectToAbsensiWithToken(user.Username, user.Role, user.GuruId?.ToString() ?? "0");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);

            if (user.Role == "Admin") return RedirectToAction("Admin_page", "Admin");
            if (user.Role == "guru") return RedirectToLatihan2WithToken(user.Username, user.Role, user.GuruId?.ToString() ?? "0");

            return RedirectToAction("Index", "Home");
        }

        // FIX: Action yang dipanggil oleh link "Aplikasi Absensi" di Sidebar
        [HttpGet("Auth/GoToAbsensi")]
        public IActionResult GoToAbsensi()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
                return RedirectToAction("Login", new { from = "absensi" });

            var username = User.Identity.Name ?? "UnknownUser";
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "guru";
            var guruid = User.FindFirst("guruid")?.Value ?? "0";

            return RedirectToAbsensiWithToken(username, role, guruid);
        }

        // URL: /Auth/Denied
        [HttpGet("Auth/Denied")]
        public IActionResult Denied()
        {
            // Jika user sudah login tapi terlempar ke sini, berarti role tidak cocok
            if (User.Identity?.IsAuthenticated == true)
            {
                ViewBag.Error = "Akses Ditolak: Akun Anda tidak memiliki izin untuk halaman Admin ini.";
            }
            else
            {
                ViewBag.Error = "Akses Ditolak: Silakan login terlebih dahulu.";
            }

            return View("Login");
        }

        // URL: /Auth/Logout
        [HttpGet("Auth/Logout")]
        public async Task<IActionResult> Logout(string source = "")
        {
            await HttpContext.SignOutAsync("CookieSekolah");
            foreach (var cookieKey in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookieKey, new CookieOptions { Path = "/" });
            }
            HttpContext.Session.Clear();

            // Skenario SSO Logout
            if (source == "guru" || source == "absensi") return RedirectToAction("Login");

            return RedirectToAction("Login");
        }

        // --- HELPER METHODS ---

        private IActionResult RedirectToAbsensiWithToken(string username, string role, string guruid)
        {
            var tokenString = GenerateJwtToken(username, role, guruid);
            var absensiBaseUrl = (_config["Latihan3:BaseUrl"] ?? "http://localhost:5035").TrimEnd('/');
            return Redirect($"{absensiBaseUrl}/Auth/SsoLogin?token={tokenString}");
        }

        private IActionResult RedirectToLatihan2WithToken(string username, string role, string guruid)
        {
            var tokenString = GenerateJwtToken(username, role, guruid);
            var latihan2BaseUrl = (_config["Latihan2:BaseUrl"] ?? "http://localhost:5001").TrimEnd('/');
            return Redirect($"{latihan2BaseUrl}/Auth/SsoLogin?token={tokenString}");
        }

        private string GenerateJwtToken(string username, string role, string guruid)
        {
            var secretKey = _config["JwtSettings:SecretKey"];
            var keyBytes = Encoding.UTF8.GetBytes(secretKey!);
            var claims = new List<Claim>
            {
                new Claim("username", username),
                new Claim("role", role),
                new Claim("guruid", guruid)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        [HttpGet("Auth/SsoAcceptor")]
        [AllowAnonymous]
        public async Task<IActionResult> SsoAcceptor(string token)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");

            try
            {
                var secretKey = _config["JwtSettings:SecretKey"];
                var keyBytes = Encoding.UTF8.GetBytes(secretKey!);
                var tokenHandler = new JwtSecurityTokenHandler();

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var username = jwtToken.Claims.First(x => x.Type == "username").Value;
                var role = jwtToken.Claims.First(x => x.Type == "role").Value;
                var guruid = jwtToken.Claims.FirstOrDefault(x => x.Type == "guruid")?.Value;

                // Buat Sesi Cookie untuk Latihan1
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };
                if (!string.IsNullOrEmpty(guruid)) claims.Add(new Claim("guruid", guruid));

                var Identity = new ClaimsIdentity(claims, "CookieSekolah");
                await HttpContext.SignInAsync("CookieSekolah", new ClaimsPrincipal(Identity));

                // Setelah sukses buat cookie Latihan1, lempar ke Admin Page
                return RedirectToAction("Admin_page", "Admin");
            }
            catch
            {
                return RedirectToAction("Login");
            }
        }
    }
}