using Latihan1.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

var sharedKeyFolder = Path.Combine(builder.Environment.ContentRootPath, "CommonKeys");
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
    });

// MVC
builder.Services.AddControllersWithViews();

// Dapper & PasswordHasher
builder.Services.AddSingleton<DapperDb>();
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();

// Session Service
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("GuruOnly", p => p.RequireRole("Guru"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // Sesudah Routing, Sebelum Auth

app.UseAuthentication();
app.UseAuthorization();

// Penting untuk atribut [HttpGet("...")]
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();