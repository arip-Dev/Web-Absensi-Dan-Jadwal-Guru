using Latihan1.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using Dapper;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddSingleton(NpgsqlDataSource.Create(connStr));

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

// Dapper & passwordhasher
builder.Services.AddScoped<DapperDb>();
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
    options.AddPolicy("guruOnly", p => p.RequireRole("guru"));
});

// 1. TAMBAHKAN BARIS INI: Mendaftarkan penerjemah TimeOnly ke TimeSpan untuk Dapper
SqlMapper.AddTypeHandler(new TimeSpanHandler());

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

// ==========================================
// SEEDING DATA AWAL (Membuat Akun Admin)
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Latihan1.Services.DapperDb>();
    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<string>();

    try
    {
        Console.WriteLine("Mencoba koneksi ke database Supabase...");

        // Cek koneksi & user (Pastikan huruf U-nya kapital: GetUserByUsernameAsync)
        var existingAdmin = await db.GetUserByUsernameAsync("admin");

        if (existingAdmin == null)
        {
            var hash = hasher.HashPassword("admin", "admin123");
            await db.CreateUserAsync("admin", hash, "Admin", null);
            Console.WriteLine("SUKSES: Akun Admin default berhasil dibuat!");
        }
        else
        {
            Console.WriteLine("INFO: Akun Admin sudah ada di database.");
        }
    }
    catch (Exception ex)
    {
        // Jika terjadi error (seperti salah password), aplikasi tidak akan crash/mati!
        Console.WriteLine("\n=== ERROR DATABASE TERDETEKSI ===");
        Console.WriteLine("Pesan: " + ex.Message);
        if (ex.InnerException != null)
        {
            Console.WriteLine("Detail: " + ex.InnerException.Message);
        }
        Console.WriteLine("=================================\n");
        Console.WriteLine("TIPS: Coba periksa kembali Password di dalam Connection String appsettings.json Anda!");
    }
}
// ==========================================
app.Run();

// ==============================================================
// 2. TAMBAHKAN CLASS INI DI BAGIAN PALING BAWAH FILE PROGRAM.CS
// ==============================================================
public class TimeSpanHandler : SqlMapper.TypeHandler<TimeSpan>
{
    public override void SetValue(IDbDataParameter parameter, TimeSpan value)
    {
        parameter.Value = value; // Biarkan Npgsql yang menangani konversi saat insert
    }

    public override TimeSpan Parse(object value)
    {
        // Jika database mengembalikan TimeOnly, ubah menjadi TimeSpan
        if (value is TimeOnly timeOnly)
            return timeOnly.ToTimeSpan();

        if (value is TimeSpan timeSpan)
            return timeSpan;

        return TimeSpan.Parse(value.ToString()!);
    }
}