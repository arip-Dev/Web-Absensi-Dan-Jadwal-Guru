using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Latihan3.Services;
using Microsoft.AspNetCore.Identity;

namespace Latihan3.Controllers
{
    [Route("Auth")]
    public class AuthController : Controller
    {
        private readonly IConfiguration _config;
        private readonly DapperDb3 _db;
        private readonly IPasswordHasher<string> _hasher; // Tambahkan hasher

        // Update Constructor untuk menerima Hasher
        public AuthController(IConfiguration config, DapperDb3 db, IPasswordHasher<string> hasher)
        {
            _config = config;
            _db = db;
            _hasher = hasher;
        }

        // 1. Pintu Gerbang Login
        // Jika user akses Latihan3 tapi belum login, middleware Cookie akan melempar ke sini.
        // Kita redirect lagi ke Latihan1 (Admin) dengan membawa parameter ?from=absensi
        [HttpGet("~/")] // Menangani http://.../absensi/
        [HttpGet("Login")] // Menangani http://.../absensi/Auth/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // 1. CEK: Apakah Cookie berhasil dibaca?
            if (User.Identity?.IsAuthenticated == true)
            {
                // Jika sudah login tapi bukan Admin (berarti guru)
                if (!User.IsInRole("Admin"))
                {
                    ViewBag.Error = "Akses Ditolak: Akun Anda (guru) tidak diizinkan mengakses halaman manajemen ini.";
                    return View();
                }

                // Jika dia Admin, biarkan masuk ke halaman yang dia tuju atau ke Index
                if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
                return RedirectToAction("Index", "Absensi");
            }

            // 2. CEK: Jika Cookie GAGAL dibaca (Anonymous) tapi mencoba akses halaman Admin
            if (!string.IsNullOrEmpty(returnUrl))
            {
                // Kita beri pesan standar login dulu
                ViewBag.Error = "Akses Ditolak: Anda perlu login terlebih dahulu untuk mengakses halaman manajemen.";
            }

            return View();
        }

        [HttpGet("Denied")]
        [Authorize] // Hanya bisa diakses oleh user yang sudah login (dalam hal ini guru)
        public IActionResult Denied()
        {
            // SKENARIO: User SUDAH LOGIN (guru) tapi mencoba menembak URL Admin
            ViewBag.Error = "Akses Ditolak: Akun Anda (guru) tidak diizinkan mengakses halaman manajemen ini.";

            // Kita kembalikan ke View Login agar user bisa melihat pesan errornya
            return View("Login");
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nip, string password)
        {
            // 1. Cek User & Password
            var storedHash = await _db.GetpasswordhashByUsernameAsync(nip);
            if (string.IsNullOrEmpty(storedHash))
            {
                ViewBag.Error = "User tidak ditemukan.";
                return View();
            }

            var result = _hasher.VerifyHashedPassword(nip, storedHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Password salah.";
                return View();
            }

            // 2. CEK role DARI DATABASE (Kunci Keamanan)
            var role = await _db.GetUserRoleAsync(nip);
            if (role != "Admin")
            {
                // Jika yang login adalah guru, blokir di sini!
                ViewBag.Error = "Akses Ditolak: Akun guru tidak diizinkan mengakses Absensi.";
                return View();
            }

            // 3. Jika Lolos (Berarti dia Admin)
            int? guruid = await _db.GetguruidBynipAsync(nip);
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, nip),
        new Claim(ClaimTypes.Role, "Admin"), // Set as Admin
        new Claim("guruid", guruid?.ToString() ?? "0")
    };

            var Identity = new ClaimsIdentity(claims, "CookieSekolah");
            await HttpContext.SignInAsync("CookieSekolah", new ClaimsPrincipal(Identity));

            return RedirectToAction("Index", "Absensi");
        }

        // 2. Menerima Token dari Latihan1
        [HttpGet("SsoLogin")]
        [AllowAnonymous]
        public async Task<IActionResult> SsoLogin(string token)
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

                if (role != "Admin")
                {
                    // Jika guru mencoba masuk, lempar ke halaman Denied atau Login dengan pesan error
                    ViewBag.Error = "Akses Ditolak: Halaman ini hanya untuk Admin.";
                    return View("Login");
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, role)
                };

                var Identity = new ClaimsIdentity(claims, "CookieSekolah");
                await HttpContext.SignInAsync("CookieSekolah", new ClaimsPrincipal(Identity));

                return RedirectToAction("Index", "Absensi");
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Token SSO tidak valid: " + ex.Message;
                return View("Login");
            }
        }

        // ================= BACK TO ADMIN =================
        [HttpGet("BackToAdmin")]
        public IActionResult BackToAdmin()
        {
            // 1. Ambil data user yang sedang login di Latihan3
            var username = User.Identity?.Name;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            var guruid = User.FindFirst("guruid")?.Value ?? "0";

            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login");

            // 2. Buat Token JWT (Gunakan SecretKey yang sama dengan Latihan1)
            var token = GenerateJwtToken(username, role ?? "Admin", guruid);

            // 3. Arahkan ke endpoint penerima token di Latihan1
            var loginUrl = _config["Latihan1:LoginUrl"]; // "http://.../Auth/Login"
            var baseUrl = loginUrl.Replace("/Auth/Login", "");

            // Kita arahkan ke endpoint SsoAcceptor (yang akan kita buat di Latihan1)
            return Redirect($"{baseUrl}/Auth/SsoAcceptor?token={token}");
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

        // ================= LOGOUT =================
        // HAPUS tanda "/" di depan -> [HttpGet("Logout")]
        [HttpGet("Logout")]
        public async Task<IActionResult> Logout(string source = "")
        {
            // 1. SignOut Standar
            await HttpContext.SignOutAsync("CookieSekolah");

            // 2. HAPUS COOKIE SECARA PAKSA (ROOT & SUB-FOLDER)
            foreach (var cookieKey in Request.Cookies.Keys)
            {
                // Hapus di root "/"
                Response.Cookies.Delete(cookieKey, new CookieOptions
                {
                    Path = "/",
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });

                // Hapus di folder "/absensi" (SANGAT PENTING)
                // Karena Latihan3 hidup di folder ini, cookienya sering nyangkut disini
                Response.Cookies.Delete(cookieKey, new CookieOptions
                {
                    Path = "/absensi",
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
            }
            HttpContext.Session.Clear();

            // 3. LOGIKA REDIRECT KE ADMIN
            var urlLoginAdmin = _config["Latihan1:LoginUrl"];
            if (string.IsNullOrEmpty(urlLoginAdmin)) return RedirectToAction("Login");

            var urlLogoutAdmin = urlLoginAdmin.Replace("/Login", "/Logout", StringComparison.OrdinalIgnoreCase);

            // Jika logout dipicu oleh Admin, kembalikan ke Login Admin
            if (source == "admin")
            {
                return Redirect(urlLoginAdmin);
            }

            // Jika logout dipicu User Absensi, lapor ke Admin
            return RedirectToAction("Login", "Auth");
        }
    }
}