using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. REGISTER SERVICES (BAGIAN ATAS)
// ==========================================

// Tentukan path folder di hosting SmarterASP yang bisa diakses bersama
// Biasanya satu level di atas folder wwwroot
var sharedKeyFolder = Path.Combine(builder.Environment.ContentRootPath, "..", "CommonKeys");

// 2. Buat folder jika tidak ada
if (!Directory.Exists(sharedKeyFolder))
{
    Directory.CreateDirectory(sharedKeyFolder);
}

// 3. Konfigurasi agar kedua aplikasi menggunakan kunci yang sama
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(sharedKeyFolder))
    .SetApplicationName("SharedSekolahApp"); // WAJIB SAMA di Latihan1 dan Latihan3

// 4. Pastikan konfigurasi Cookie Anda identik
builder.Services.AddAuthentication("CookieSekolah")
    .AddCookie("CookieSekolah", options =>
    {
        options.Cookie.Name = "SharedSekolahCookie";
        options.Cookie.Path = "/"; // Agar cookie dari root bisa dibaca sub-folder
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Denied";
    }); // Nama ini HARUS SAMA di ketiga project

builder.Services.AddControllersWithViews();

// --- PENTING: Service Session ---
builder.Services.AddDistributedMemoryCache(); // 1. Wajib ada
builder.Services.AddSession(options =>        // 2. Konfigurasi Session
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Daftarkan DapperDb3
builder.Services.AddSingleton<Latihan3.Services.DapperDb3>();
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
var app = builder.Build();

// ==========================================
// 2. CONFIGURE PIPELINE (BAGIAN BAWAH)
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Beritahu aplikasi bahwa dia berjalan di dalam folder "/absensi"
app.UsePathBase("/absensi");

app.UseHttpsRedirection(); // Matikan jika develop di HTTP lokal
app.UseStaticFiles();

app.UseRouting();

// --- PENTING: Middleware Session ---
// WAJIB Ditaruh SETELAH UseRouting dan SEBELUM UseAuthentication
app.UseSession();  // <--- INI YANG KEMUNGKINAN ANDA LEWATKAN

app.UseAuthentication();
app.UseAuthorization();

// UTAMAKAN MapControllers agar atribut [HttpGet("~/")] diproses lebih dulu
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();