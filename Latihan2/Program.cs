using Latihan2.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

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
    });
builder.Services.AddControllersWithViews();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = "Sekolah.Session"; // Disamakan prefixnya
    o.IdleTimeout = TimeSpan.FromHours(4);
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddSingleton<DapperDb2>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Urutan PathBase sangat penting
app.UsePathBase("/guru");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Auth", action = "Login" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();