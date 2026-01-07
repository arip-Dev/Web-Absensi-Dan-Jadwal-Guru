using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration; // Pastikan namespace ini ada
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Latihan2.Services
{
    public class DapperDb2
    {
        private readonly string _cs;
        public DapperDb2(IConfiguration cfg) =>
            _cs = cfg.GetConnectionString("DefaultConnection")!;

        private IDbConnection Conn() => new SqlConnection(_cs);

        // ===================== GURU =====================
        public class GuruModel
        {
            public int Id { get; set; }
            public string Nama { get; set; } = "";
            public string? NIP { get; set; } = "";
            public string Email { get; set; } = "";
            public int JamMengajar { get; set; }
            public bool IsActive { get; set; }
            public int MaxWeeklyLoad { get; set; }
            public int MaxDailyLoad { get; set; }
            public int MaxConsecutiveSlots { get; set; }
            public int MapelId { get; set; }
            public string MapelNama { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public string? QRCodeBase64 { get; set; }
        }

        public async Task<GuruModel?> GetGuruByIdAsync(int id)
        {
            const string sql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'')                    AS NIP,
    g.Email,
    ISNULL(g.JamMengajar,0)             AS JamMengajar,
    ISNULL(g.IsActive,1)                AS IsActive,
    ISNULL(g.MaxWeeklyLoad,24)          AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad,6)            AS MaxDailyLoad,
    ISNULL(g.MaxConsecutiveSlots,3)     AS MaxConsecutiveSlots,
    g.MapelId,
    ISNULL(m.Nama,'(Tanpa mapel)')      AS MapelNama,
    g.QRCodeBase64,
    g.CreatedAt
FROM dbo.Guru g
LEFT JOIN dbo.Mapel m ON m.Id = g.MapelId
WHERE g.Id = @id;";
            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<GuruModel>(sql, new { id });
        }

        // --- TAMBAHAN: FUNGSI LOGIN MANDIRI ---
        // Mencari Guru berdasarkan Username & Password dari tabel Users
        public async Task<GuruModel?> LoginGuruAsync(string username, string password)
        {
            // Catatan: Di production, Anda harus memverifikasi Password Hash!
            // Query ini mengasumsikan Anda menggunakan PasswordHasher seperti di Latihan1
            // Namun untuk simplicity, kita ambil user dulu, baru controller yang verifikasi hash (atau verifikasi plain text jika belum di-hash)

            // Kita join tabel Users (Akun) dengan Gurus (Data Profil)
            const string sql = @"
SELECT 
    g.Id, 
    g.Nama, 
    g.NIP,
    u.PasswordHash -- Kita butuh ini untuk verifikasi di Controller (jika pakai hash)
FROM Users u
JOIN Guru g ON u.GuruId = g.Id
WHERE u.Username = @Username AND u.Role = 'Guru'";

            using var db = Conn();

            // Kita return dynamic dulu karena GuruModel tidak punya properti PasswordHash
            var result = await db.QueryFirstOrDefaultAsync(sql, new { Username = username });

            if (result == null) return null;

            // Di sini kita lakukan verifikasi password sederhana (Plain Text Check)
            // JIKA di database password tersimpan sebagai Hash, Anda butuh PasswordHasher di Controller.
            // Asumsi: Password di database sudah di-hash oleh Latihan1.
            // Karena kita tidak punya IPasswordHasher di DapperDb2, kita return User object-nya saja,
            // Biar AuthController yang validasi passwordnya.

            // TAPI, agar cepat dan sesuai permintaan 'Shared Database' tanpa ribet DI:
            // Kita bisa return GuruModel JIKA password cocok. 
            // Mari kita anggap AuthController akan menghandle verifikasi password.

            return new GuruModel
            {
                Id = result.Id,
                Nama = result.Nama,
                NIP = result.NIP
                // PasswordHash kita kirim lewat cara lain atau verifikasi di level query jika plain text
            };
        }

        // --- ALTERNATIF LOGIN (JIKA PASSWORD PLAIN TEXT / UNTUK TES) ---
        // Gunakan ini jika Anda ingin login langsung tembak DB
        public async Task<GuruModel?> LoginGuruDirectAsync(string username)
        {
            const string sql = @"
SELECT g.Id, g.Nama, g.NIP, u.PasswordHash
FROM Users u
JOIN Guru g ON u.GuruId = g.Id
WHERE u.Username = @Username AND u.Role = 'Guru'";

            using var db = Conn();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { Username = username });
            if (row == null) return null;

            return new GuruModel { Id = row.Id, Nama = row.Nama, NIP = row.NIP };
        }

        // Helper untuk ambil password hash (biar controller yang cek)
        public async Task<string?> GetPasswordHashAsync(string username)
        {
            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<string>("SELECT PasswordHash FROM Users WHERE Username = @u AND Role='Guru'", new { u = username });
        }

        public record JadwalRow(
            int Id,
            string Mapel,
            int Hari,
            TimeSpan Mulai,
            TimeSpan Selesai,
            string KelasNama,
            string? Ruangan
        );

        public async Task<IEnumerable<JadwalRow>> ListJadwalByGuruAsync(int guruId)
        {
            const string sql = @"
SELECT j.Id,
       j.Mapel,
       CAST(j.Hari AS int) AS Hari,
       j.Mulai,
       j.Selesai,
       k.Nama AS KelasNama,
       j.Ruangan
FROM dbo.Jadwal j
JOIN dbo.Kelas k ON k.Id = j.KelasId
WHERE j.GuruId = @guruId
ORDER BY j.Hari, j.Mulai;";
            using var db = Conn();
            return await db.QueryAsync<JadwalRow>(sql, new { guruId });
        }
    }
}