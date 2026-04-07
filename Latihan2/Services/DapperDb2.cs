using Dapper;
using Npgsql;
using System.Data;
using Microsoft.Extensions.Configuration;
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

        // NpgsqlConnection implement IAsyncDisposable + punya OpenAsync
        private NpgsqlConnection Conn() => new NpgsqlConnection(_cs);

        // ===================== GURU =====================
        public class GuruModel
        {
            public int Id { get; set; }
            public string Nama { get; set; } = "";
            public string? Nip { get; set; } = "";
            public string Email { get; set; } = "";
            public int JamMengajar { get; set; }
            public bool IsActive { get; set; }
            public int MaxWeeklyLoad { get; set; }
            public int MaxDailyLoad { get; set; }
            public int MaxConsecutiveSlots { get; set; }
            public int MapelId { get; set; }
            public string MapelNama { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public string? QrCodeBase64 { get; set; }
        }

        public async Task<GuruModel?> GetGuruByIdAsync(int id)
        {
            const string sql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'')                    AS nip,
    g.email,
    COALESCE(g.jammengajar,0)             AS jammengajar,
    COALESCE(g.isactive,true)             AS isactive,
    COALESCE(g.maxweeklyload,24)          AS maxweeklyload,
    COALESCE(g.maxdailyload,6)            AS maxdailyload,
    COALESCE(g.maxconsecutiveslots,3)     AS maxconsecutiveslots,
    g.mapelid,
    COALESCE(m.nama,'(Tanpa mapel)')      AS mapelnama,
    g.qrcodebase64,
    g.createdat
FROM guru g
LEFT JOIN mapel m ON m.id = g.mapelid
WHERE g.id = @id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<GuruModel>(sql, new { id });
        }

        // Gunakan ini untuk login (Ambil data Guru berdasarkan Username yang ada di tabel Users)
        public async Task<GuruModel?> LoginGuruDirectAsync(string username)
        {
            // PERBAIKAN: Gunakan ILIKE pada pencarian role agar kebal terhadap huruf besar/kecil ('guru' atau 'Guru' tetap ketemu)
            const string sql = @"
SELECT g.id, g.nama, g.nip, u.passwordhash
FROM users u
JOIN guru g ON u.guruid = g.id
WHERE u.username = @username AND u.role ILIKE 'guru'";

            await using var db = Conn();
            await db.OpenAsync();
            var row = await db.QueryFirstOrDefaultAsync(sql, new { username = username });

            if (row == null) return null;

            return new GuruModel { Id = row.id, Nama = row.nama, Nip = row.nip };
        }

        // Helper untuk ambil password hash (biar controller yang cek)
        public async Task<string?> GetPasswordHashAsync(string username)
        {
            await using var db = Conn();
            await db.OpenAsync();

            // PERBAIKAN: Gunakan ILIKE pada pencarian role agar kebal huruf besar/kecil
            const string sql = "SELECT passwordhash FROM users WHERE username = @u AND role ILIKE 'guru'";

            return await db.QueryFirstOrDefaultAsync<string>(sql, new { u = username });
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

        public async Task<IEnumerable<JadwalRow>> ListJadwalByGuruAsync(int guruid)
        {
            // PERHATIKAN: j.mulai::interval dan j.selesai::interval
            const string sql = @"
SELECT j.id,
       j.mapel,
       CAST(j.hari AS int) AS hari,
       j.mulai::interval AS mulai,
       j.selesai::interval AS selesai,
       k.nama AS kelasnama,
       j.ruangan
FROM jadwal j
JOIN kelas k ON k.id = j.kelasid
WHERE j.guruid = @guruid
ORDER BY j.hari, j.mulai;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<JadwalRow>(sql, new { guruid });
        }
    }
}