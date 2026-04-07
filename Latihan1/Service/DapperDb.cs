using Dapper;
using Latihan1.Models;
using Npgsql;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Latihan1.Services
{
    public class DapperDb
    {
        private readonly string _cs;
        public DapperDb(IConfiguration cfg) => _cs = cfg.GetConnectionString("DefaultConnection")!;
        private NpgsqlConnection Conn() => new NpgsqlConnection(_cs);

        // =======================
        // ======== USERS ========
        // =======================
        public record UserRow(int Id, string Username, string PasswordHash, string Role, int? GuruId);

        public async Task<UserRow?> GetUserByUsernameAsync(string username)
        {
            const string sql = @"
SELECT id, username, passwordhash, role, guruid AS GuruId
FROM users
WHERE username = @username;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<UserRow>(sql, new { username });
        }

        public async Task<int?> GetUserIdByUsernameAsync(string username)
        {
            const string sql = "SELECT id FROM users WHERE username = @username;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int?>(sql, new { username });
        }

        public async Task<int> CreateUserAsync(string username, string passwordHash, string role, int? guruId)
        {
            const string sql = @"
INSERT INTO users (username, passwordhash, role, guruid)
VALUES (@username, @passwordHash, @role, @guruId)
RETURNING id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { username, passwordHash, role, guruId });
        }

        public async Task<int> UpdateUserGuruIdAsync(int userId, int? guruId)
        {
            const string sql = "UPDATE users SET guruid = @guruId WHERE id = @userId;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { userId, guruId });
        }

        public async Task<bool> GuruExistsAsync(int id)
        {
            const string sql = "SELECT CASE WHEN EXISTS(SELECT 1 FROM guru WHERE id=@id) THEN 1 ELSE 0 END";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id }) == 1;
        }

        public async Task<int> CreateUserIfNotExistsAsync(string username, string passwordHash, string role, int? guruId = null)
        {
            var existing = await GetUserIdByUsernameAsync(username);
            if (existing is not null) return 0;

            int? link = null;
            if (guruId is int gid && await GuruExistsAsync(gid)) link = gid;

            return await CreateUserAsync(username, passwordHash, role, link);
        }

        // ===== MAPEL (CRUD) =====
        public async Task<string> GenerateNextMapelCodeAsync()
        {
            const string sql = @"
SELECT COALESCE(
    MAX(CAST(SUBSTRING(kode, 3, 10) AS INTEGER)),
    0
)
FROM mapel
WHERE kode LIKE 'MP%';";

            await using var db = Conn();
            await db.OpenAsync();
            var lastNumber = await db.ExecuteScalarAsync<int>(sql);
            var nextNumber = lastNumber + 1;

            return "MP" + nextNumber.ToString("D3");
        }


        public async Task<IEnumerable<mapelModel>> GetAllMapelAsync()
        {
            const string sql = "SELECT id, kode, nama FROM mapel ORDER BY nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<mapelModel>(sql);
        }

        public async Task<mapelModel?> GetMapelByIdAsync(int id)
        {
            const string sql = "SELECT id, kode, nama FROM mapel WHERE id=@id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<mapelModel>(sql, new { id });
        }

        public async Task<int> CreateMapelAsync(mapelModel m)
        {
            const string sql = @"
INSERT INTO mapel (kode, nama) VALUES (@Kode, @Nama) RETURNING id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> UpdateMapelAsync(mapelModel m)
        {
            const string sql = "UPDATE mapel SET kode=@Kode, nama=@Nama WHERE id=@Id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> DeleteMapelAsync(int id)
        {
            const string sql = "DELETE FROM mapel WHERE id=@id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id });
        }


        // =======================
        // ========= GURU ========
        // =======================
        public async Task<guruModel?> GetGuruByIdAsync(int id)
        {
            const string sql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'') AS nip,
    g.email,
    COALESCE((
        SELECT SUM(EXTRACT(EPOCH FROM (j.selesai - j.mulai)) / 60) / 60
        FROM jadwal j 
        WHERE j.guruid = g.id
    ), 0) AS jammengajar, 
    COALESCE(g.isactive, true) AS isactive,
    COALESCE(g.maxweeklyload, 24) AS maxweeklyload,
    COALESCE(g.maxdailyload, 6) AS maxdailyload,
    g.mapelid,
    g.qrcodebase64,
    m.nama AS mapelnama,
    g.createdat
FROM guru g
JOIN mapel m ON m.id = g.mapelid
WHERE g.id = @id;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<guruModel>(sql, new { id });
        }

        public async Task<bool> GuruHasJadwalAsync(int guruId)
        {
            const string sql = @"
SELECT CASE WHEN EXISTS(
    SELECT 1 FROM jadwal WHERE guruid = @guruId
) THEN 1 ELSE 0 END;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { guruId }) == 1;
        }

        public Task<guruModel?> GetGuruForEditAsync(int id) => GetGuruByIdAsync(id);

        public async Task<IEnumerable<guruModel>> SearchGuruAsync(string? q)
        {
            const string sql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'') AS nip,
    g.email,
    COALESCE(g.jammengajar,0) AS jammengajar,
    COALESCE(g.isactive,true)    AS isactive,
    COALESCE(g.maxweeklyload,24)      AS maxweeklyload,
    COALESCE(g.maxdailyload,6)        AS maxdailyload,
    COALESCE(g.maxconsecutiveslots,3) AS maxconsecutiveslots,
    g.mapelid,
    g.qrcodebase64,
    STRING_AGG(m.nama, ', ' ORDER BY m.nama) AS mapelnama,
    g.createdat
FROM guru g
LEFT JOIN gurumapel gm ON gm.guruid = g.id
LEFT JOIN mapel     m  ON m.id      = gm.mapelid
WHERE (@q IS NULL OR @q = '')
   OR (g.nama ILIKE '%' || @q || '%'
    OR g.nip   ILIKE '%' || @q || '%'
    OR m.nama  ILIKE '%' || @q || '%'
    OR g.email ILIKE '%' || @q || '%')
GROUP BY
    g.id,
    g.nama,
    g.nip,
    g.email,
    g.jammengajar,
    g.isactive,
    g.maxweeklyload,
    g.maxdailyload,
    g.maxconsecutiveslots,
    g.mapelid,
    g.qrcodebase64,
    g.createdat
ORDER BY g.nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<guruModel>(sql, new { q });
        }

        public async Task<(IEnumerable<guruModel> Items, int Total)> SearchGuruPagedAsync(string? q, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            int skip = (page - 1) * pageSize;

            const string countSql = @"
SELECT COUNT(DISTINCT g.id)
FROM guru g
LEFT JOIN gurumapel gm ON gm.guruid = g.id
LEFT JOIN mapel     m  ON m.id      = gm.mapelid
WHERE (@q IS NULL OR @q = '')
   OR (g.nama  ILIKE '%' || @q || '%'
    OR g.nip   ILIKE '%' || @q || '%'
    OR m.nama  ILIKE '%' || @q || '%'
    OR g.email ILIKE '%' || @q || '%');";

            const string dataSql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'') AS nip,
    g.email,
    COALESCE((
        SELECT SUM(EXTRACT(EPOCH FROM (j.selesai - j.mulai)) / 60) / 60
        FROM jadwal j 
        WHERE j.guruid = g.id
    ), 0) AS jammengajar, 
    COALESCE(g.isactive,true)    AS isactive,
    COALESCE(g.maxweeklyload,24)      AS maxweeklyload,
    COALESCE(g.maxdailyload,6)        AS maxdailyload,
    COALESCE(g.maxconsecutiveslots,3) AS maxconsecutiveslots,
    g.mapelid,
    g.qrcodebase64,
    STRING_AGG(m.nama, ', ' ORDER BY m.nama) AS mapelnama,
    g.createdat
FROM guru g
LEFT JOIN gurumapel gm ON gm.guruid = g.id
LEFT JOIN mapel      m  ON m.id      = gm.mapelid
WHERE (@q IS NULL OR @q = '')
   OR (g.nama  ILIKE '%' || @q || '%'
    OR g.nip   ILIKE '%' || @q || '%'
    OR m.nama  ILIKE '%' || @q || '%'
    OR g.email ILIKE '%' || @q || '%')
GROUP BY
    g.id, g.nama, g.nip, g.email, g.isactive, g.maxweeklyload, 
    g.maxdailyload, g.maxconsecutiveslots, g.mapelid, g.qrcodebase64, g.createdat
ORDER BY g.nama
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";

            await using var db = Conn();
            await db.OpenAsync();
            var total = await db.ExecuteScalarAsync<int>(countSql, new { q });
            var items = await db.QueryAsync<guruModel>(dataSql, new { q, skip, take = pageSize });
            return (items, total);
        }

        public async Task<int> CreateGuruAsync(guruModel m)
        {
            const string sql = @"
INSERT INTO guru
    (nama, nip, email, jammengajar, isactive,
     maxweeklyload, maxdailyload, maxconsecutiveslots, mapelid, qrcodebase64, createdat)
VALUES
    (@Nama, @nip, @Email, @JamMengajar, @IsActive,
     @MaxWeeklyLoad, @MaxDailyLoad, @MaxConsecutiveSlots, @MapelId, @qrcodebase64, CURRENT_TIMESTAMP)
RETURNING id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> UpdateGuruAsync(guruModel m)
        {
            const string sql = @"
UPDATE guru
SET nama=@Nama,
    nip=@nip,
    email=@Email,
    isactive=@IsActive,
    maxweeklyload=@MaxWeeklyLoad,
    maxdailyload=@MaxDailyLoad,
    mapelid=@MapelId,
    qrcodebase64=@qrcodebase64
WHERE id=@Id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> DeleteGuruAsync(int id)
        {
            const string sql = "DELETE FROM guru WHERE id=@id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id });
        }

        public async Task<int> DeleteGuruAndDetachUsersAsync(int id, bool deleteUsers = true)
        {
            await using var db = Conn();
            await db.OpenAsync();

            // Gabungkan semua perintah ke dalam SATU string query.
            // PostgreSQL akan otomatis mengeksekusi ini sebagai satu transaksi penuh (All-or-Nothing).
            // Ini mem-bypass total penggunaan NpgsqlTransaction yang menyebabkan crash.
            string sql;

            if (deleteUsers)
            {
                sql = @"
            DELETE FROM gurumapel WHERE guruid = @id;
            DELETE FROM users WHERE guruid = @id;
            DELETE FROM guru WHERE id = @id;
        ";
            }
            else
            {
                sql = @"
            DELETE FROM gurumapel WHERE guruid = @id;
            UPDATE users SET guruid = NULL WHERE guruid = @id;
            DELETE FROM guru WHERE id = @id;
        ";
            }

            // Eksekusi semua sekaligus dalam satu round-trip ke database
            return await db.ExecuteAsync(sql, new { id });
        }

        // =======================================
        // === RELASI GURU – MAPEL (banyak2) ====
        // =======================================

        public async Task<IEnumerable<int>> GetMapelIdsForGuruAsync(int guruId)
        {
            const string sql = @"SELECT mapelid FROM gurumapel WHERE guruid = @guruId;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<int>(sql, new { guruId });
        }

        public async Task SaveGuruMapelAsync(int guruId, int[] mapelIds)
        {
            await using var db = Conn();
            await db.OpenAsync();

            // Eksekusi DELETE sepenuhnya dan tunggu sampai benar-benar selesai
            const string delSql = "DELETE FROM gurumapel WHERE guruid = @guruId;";
            await db.ExecuteAsync(delSql, new { guruId });

            // Jika tidak ada mapel yang dipilih, proses berhenti di sini
            if (mapelIds == null || mapelIds.Length == 0) return;

            // Eksekusi INSERT satu per satu dengan pelindung ON CONFLICT DO NOTHING
            const string insSql = "INSERT INTO gurumapel (guruid, mapelid) VALUES (@guruId, @mapelId) ON CONFLICT DO NOTHING;";
            var uniqueMapelIds = mapelIds.Distinct().ToList();

            foreach (var mid in uniqueMapelIds)
            {
                await db.ExecuteAsync(insSql, new { guruId = guruId, mapelId = mid });
            }
        }

        public async Task<int> DeleteGuruMapelAsync(int guruId)
        {
            const string sql = @"DELETE FROM gurumapel WHERE guruid = @guruId;";
            await using var db = Conn();
            await db.OpenAsync();
            // Gunakan ExecuteAsync (bukan ExecuteScalarAsync) untuk perintah DELETE
            return await db.ExecuteAsync(sql, new { guruId });
        }

        // ==============================
        // ===== SUBJECTS (MAPEL) ======
        // ==============================
        public sealed record SubjectCountRow(int MapelId, string Mapel, int Count);

        public async Task<IEnumerable<SubjectCountRow>> GetSubjectsWithCountsAsync()
        {
            const string sql = @"
SELECT m.id AS MapelId, m.nama AS Mapel, CAST(COUNT(*) AS int) AS ""Count""
FROM guru g
JOIN mapel m ON m.id = g.mapelid
GROUP BY m.id, m.nama
ORDER BY m.nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<SubjectCountRow>(sql);
        }

        public async Task<IEnumerable<string>> GetDistinctMapelAsync()
        {
            const string sql = @"SELECT nama FROM mapel ORDER BY nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<guruModel>> GetTeachersBySubjectIdAsync(int mapelId)
        {
            const string sql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'') AS nip,
    g.email,
    COALESCE(g.jammengajar,0) AS jammengajar,
    COALESCE(g.isactive,true)    AS isactive,
    COALESCE(g.maxweeklyload,24)      AS maxweeklyload,
    COALESCE(g.maxdailyload,6)        AS maxdailyload,
    COALESCE(g.maxconsecutiveslots,3) AS maxconsecutiveslots,
    g.mapelid,
    g.qrcodebase64,
    m.nama AS mapelnama,
    g.createdat
FROM guru g
JOIN mapel m ON m.id = g.mapelid
WHERE g.mapelid = @mapelId
ORDER BY g.nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<guruModel>(sql, new { mapelId });
        }

        public async Task<IEnumerable<guruModel>> GetTeachersBySubjectAsync(string mapelNama)
        {
            const string sql = @"
SELECT 
    g.id,
    g.nama,
    COALESCE(g.nip,'') AS nip,
    g.email,
    COALESCE(g.jammengajar,0) AS jammengajar,
    COALESCE(g.isactive,true)    AS isactive,
    COALESCE(g.maxweeklyload,24)      AS maxweeklyload,
    COALESCE(g.maxdailyload,6)        AS maxdailyload,
    COALESCE(g.maxconsecutiveslots,3) AS maxconsecutiveslots,
    g.mapelid,
    g.qrcodebase64,
    m.nama AS mapelnama,
    g.createdat
FROM guru g
JOIN mapel m ON m.id = g.mapelid
WHERE m.nama = @mapelNama
ORDER BY g.nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<guruModel>(sql, new { mapelNama });
        }

        // =======================
        // ========= KELAS =======
        // =======================
        public record KelasRow(int Id, string Tingkat, string Nama, bool IsActive, DateTime CreatedAt);

        public async Task<IEnumerable<string>> GetAllGradesAsync()
        {
            const string sql = @"
SELECT DISTINCT tingkat
FROM kelas
WHERE isactive = true
ORDER BY tingkat;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<KelasRow>> GetKelasByGradeAsync(string tingkat)
        {
            const string sql = @"
SELECT id, tingkat, nama, COALESCE(isactive, true) AS isactive, createdat
FROM kelas
WHERE tingkat = @tingkat
ORDER BY nama;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<KelasRow>(sql, new { tingkat });
        }

        public async Task<int> InsertKelasAsync(string tingkat, string nama, bool isActive = true)
        {
            const string sql = @"
INSERT INTO kelas (tingkat, nama, isactive, createdat)
VALUES (@tingkat, @nama, @isActive, CURRENT_TIMESTAMP)
RETURNING id;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { tingkat, nama, isActive });
        }

        public async Task<int> UpdateKelasAsync(int id, string tingkat, string nama, bool isActive)
        {
            const string sql = @"
UPDATE kelas
SET tingkat = @tingkat,
    nama    = @nama,
    isactive = @isActive
WHERE id = @id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id, tingkat, nama, isActive });
        }

        public async Task<int> DeleteKelasAsync(int id)
        {
            const string sql = @"DELETE FROM kelas WHERE id = @id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id });
        }

        public async Task<IEnumerable<KelasRow>> GetKelasListAsync()
        {
            const string sql = @"
SELECT id, tingkat, nama, COALESCE(isactive, true) AS isactive, createdat
FROM kelas
ORDER BY tingkat, nama;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<KelasRow>(sql);
        }

        public record KelasGroupRow(string Tingkat, int Count);
        public async Task<IEnumerable<KelasGroupRow>> GetKelasGroupsAsync()
        {
            const string sql = @"
SELECT tingkat, CAST(COUNT(*) AS int) AS ""Count""
FROM kelas
GROUP BY tingkat
ORDER BY tingkat;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<KelasGroupRow>(sql);
        }

        public Task<IEnumerable<KelasRow>> GetKelasByTingkatAsync(string tingkat)
            => GetKelasByGradeAsync(tingkat);

        public async Task<KelasRow?> GetKelasByIdAsync(int id)
        {
            const string sql = @"
SELECT id, tingkat, nama, COALESCE(isactive, true) AS isactive, createdat
FROM kelas
WHERE id = @id;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<KelasRow>(sql, new { id });
        }

        public Task<int> CreateKelasAsync(kelasModel m)
            => InsertKelasAsync(m.tingkat!, m.nama!, m.isactive);

        public Task<int> UpdateKelasAsync(kelasModel m)
            => UpdateKelasAsync(m.id, m.tingkat!, m.nama!, m.isactive);

        public async Task<int> UpdateKelasAsync(int id, string nama, bool isActive)
        {
            var tingkat = await GetKelasTingkatAsyncInternal(id);
            return await UpdateKelasAsync(id, tingkat, nama, isActive);
        }

        private async Task<string> GetKelasTingkatAsyncInternal(int id)
        {
            const string sql = "SELECT tingkat FROM kelas WHERE id=@id LIMIT 1;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<string>(sql, new { id }) ?? "X";
        }

        public Task<int> CreateKelasAsync(string tingkat, string nama, bool isActive = true)
            => InsertKelasAsync(tingkat, nama, isActive);

        public Task<int> CreateKelasAsync(string tingkat)
            => InsertKelasAsync(tingkat, $"{tingkat} 1", true);

        // ==========================
        // ========= JADWAL =========
        // ==========================
        public class JadwalRow
        {
            public int Id { get; set; }
            public int GuruId { get; set; }
            public string GuruNama { get; set; } = "";
            public string Mapel { get; set; } = "";
            public int Hari { get; set; }
            public TimeSpan Mulai { get; set; }
            public TimeSpan Selesai { get; set; }
            public int KelasId { get; set; }
            public string KelasNama { get; set; } = "";
            public string? Ruangan { get; set; }
        }

        private static int DayNameToNum(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 1;
            return name.Trim().ToLowerInvariant() switch
            {
                "senin" => 1,
                "selasa" => 2,
                "rabu" => 3,
                "kamis" => 4,
                "jumat" or "jum'at" => 5,
                "sabtu" => 6,
                "minggu" => 7,
                _ => int.TryParse(name, out var x) ? Math.Clamp(x, 1, 7) : 1
            };
        }

        public async Task<bool> HasScheduleConflictAsync(
            int guruId, int hari, TimeSpan mulai, TimeSpan selesai, int? excludeId = null)
        {
            const string sql = @"
SELECT 1
FROM jadwal j
WHERE j.guruid = @guruId
  AND j.hari   = @hari
  AND (@mulai  < j.selesai AND @selesai > j.mulai)
  AND (@excludeId IS NULL OR j.id <> @excludeId);";

            await using var db = Conn();
            await db.OpenAsync();
            var exists = await db.ExecuteScalarAsync<int?>(
                sql, new { guruId, hari, mulai, selesai, excludeId });

            return exists.HasValue;
        }

        public async Task<int> CreateJadwalAsync(
            int guruId, string mapel, int hari, TimeSpan mulai, TimeSpan selesai, int kelasId, string? ruangan)
        {
            const string sql = @"
INSERT INTO jadwal (guruid, mapel, hari, mulai, selesai, kelasid, ruangan)
VALUES (@guruId, @mapel, @hari, @mulai, @selesai, @kelasId, @ruangan)
RETURNING id;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                guruId,
                mapel,
                hari,
                mulai,
                selesai,
                kelasId,
                ruangan
            });
        }

        public async Task<int> UpdateJadwalAsync(
            int id,
            int guruId,
            string mapel,
            int hari,
            TimeSpan mulai,
            TimeSpan selesai,
            int kelasId,
            string? ruangan)
        {
            const string sql = @"
UPDATE jadwal
SET guruid = @guruId,
    mapel  = @mapel,
    hari   = @hari,
    mulai  = @mulai,
    selesai = @selesai,
    kelasid = @kelasId,
    ruangan = @ruangan
WHERE id = @id;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                id,
                guruId,
                mapel,
                hari,
                mulai,
                selesai,
                kelasId,
                ruangan
            });
        }

        public async Task<IEnumerable<JadwalRow>> ListJadwalAsync(int? hari = null)
        {
            const string sql = @"
SELECT  j.id,
        j.guruid,
        COALESCE(g.nama,'(guru tidak ada)')                 AS GuruNama,
        COALESCE(j.mapel, m.nama, '(Tanpa mapel)')          AS Mapel, 
        CAST(j.hari AS int)                                 AS Hari,
        j.mulai,
        j.selesai,
        j.kelasid,
        -- PERBAIKAN: Menggabungkan tingkat dan nama kelas (Contoh: 'X IPA1')
        COALESCE(k.tingkat || ' ' || k.nama, '(kelas tidak ada)') AS KelasNama,
        j.ruangan
FROM jadwal j
LEFT JOIN guru  g ON g.id = j.guruid
LEFT JOIN kelas k ON k.id = j.kelasid
LEFT JOIN mapel m ON m.id = g.mapelid            
WHERE (@hari IS NULL OR j.hari = @hari)
ORDER BY j.hari, j.mulai;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<JadwalRow>(sql, new { hari });
        }

        public async Task<int> DeleteJadwalAsync(int id)
        {
            const string sql = @"DELETE FROM jadwal WHERE id = @id;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { id });
        }

        public async Task<IEnumerable<JadwalRow>> ListJadwalAsync(string? hari)
        {
            int? h = string.IsNullOrWhiteSpace(hari) ? null : DayNameToNum(hari);
            return await ListJadwalAsync(h);
        }

        public Task<IEnumerable<JadwalRow>> GetAllJadwalAsync(int? hari = null)
            => ListJadwalAsync(hari);

        public Task<IEnumerable<JadwalRow>> GetAllJadwalAsync(string? hari = null)
            => ListJadwalAsync(string.IsNullOrWhiteSpace(hari) ? null : DayNameToNum(hari));

        // === GURU + LOAD ===
        public record GuruLoadRow(int Id, string Guru, string Mapel, int MaxWeeklyLoad, int CurrentLoad);

        public async Task<IEnumerable<GuruLoadRow>> GetGuruByMapelWithLoadAsync(string mapelNama)
        {
            const string sql = @"
            SELECT 
                g.id AS Id,
                g.nama AS Guru,
                m.nama AS Mapel,                                   
                CAST(COALESCE(g.maxweeklyload, 24) AS integer) AS MaxWeeklyLoad,
                CAST(COALESCE((
                    SELECT SUM(EXTRACT(EPOCH FROM (j.selesai - j.mulai)) / 60) 
                    FROM jadwal j 
                    WHERE j.guruid = g.id
                ), 0) AS integer) AS CurrentLoad
            FROM guru g
            JOIN gurumapel gm ON gm.guruid = g.id         
            JOIN mapel m      ON m.id      = gm.mapelid
            WHERE LOWER(TRIM(m.nama)) = LOWER(TRIM(@mapelNama))
            ORDER BY g.nama;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<GuruLoadRow>(sql, new { mapelNama });
        }

        public Task<bool> HasTimeConflictAsync(int guruId, string hari, TimeSpan mulai, TimeSpan selesai, int? excludeId = null)
            => HasScheduleConflictAsync(guruId, DayNameToNum(hari), mulai, selesai, excludeId);
        public Task<bool> HasTimeConflictAsync(int guruId, TimeSpan mulai, string hari, TimeSpan selesai, int? excludeId = null)
            => HasScheduleConflictAsync(guruId, DayNameToNum(hari), mulai, selesai, excludeId);
        public Task<bool> HasTimeConflictAsync(int guruId, int hari, TimeSpan mulai, TimeSpan selesai, int? excludeId = null)
            => HasScheduleConflictAsync(guruId, hari, mulai, selesai, excludeId);

        // == JADWAL PER KELAS ==
        public async Task<IEnumerable<JadwalRow>> ListJadwalByKelasAsync(int kelasId)
        {
            const string sql = @"
SELECT j.id,
       j.guruid,
       COALESCE(g.nama,'(guru tidak ada)') AS GuruNama,
       COALESCE(j.mapel,'(Tanpa mapel)')   AS Mapel,
       CAST(j.hari AS int)                 AS Hari,
       j.mulai,
       j.selesai,
       j.kelasid,
       -- PERBAIKAN: Menggabungkan tingkat dan nama kelas
       COALESCE(k.tingkat || ' ' || k.nama, '(kelas tidak ada)') AS KelasNama,
       j.ruangan
FROM jadwal j
LEFT JOIN guru  g ON g.id = j.guruid
LEFT JOIN kelas k ON k.id = j.kelasid
WHERE j.kelasid = @kelasId
ORDER BY j.hari, j.mulai;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<JadwalRow>(sql, new { kelasId });
        }

        public async Task<IEnumerable<Latihan1.Models.mapelListItemVm>> GetMapelWithCountsAsync()
        {
            // KITA UBAH JOIN-NYA DARI 'guru' MENJADI 'gurumapel'
            const string sql = @"
SELECT m.id, m.kode, m.nama, CAST(COUNT(gm.guruid) AS int) AS ""Count""
FROM mapel m
LEFT JOIN gurumapel gm ON gm.mapelid = m.id
GROUP BY m.id, m.kode, m.nama
ORDER BY m.nama;";

            await using var db = Conn();
            await db.OpenAsync(); // Selalu pastikan ada OpenAsync ya!
            return await db.QueryAsync<Latihan1.Models.mapelListItemVm>(sql);
        }

        // ===== DASHBOARD HELPERS =====
        public sealed record TeacherLoadRow(string Nama, string Mapel, int LoadMinutes, int MaxWeeklyLoad);

        public async Task<IEnumerable<TeacherLoadRow>> GetTeacherLoadsAsync()
        {
            const string sql = @"
SELECT 
    g.nama,
    m.nama AS Mapel,
    COALESCE(CAST(SUM(EXTRACT(EPOCH FROM (j.selesai - j.mulai)) / 60) AS integer), 0) AS LoadMinutes,
    COALESCE(g.maxweeklyload, 24) AS MaxWeeklyLoad
FROM guru g
JOIN mapel m ON m.id = g.mapelid
LEFT JOIN jadwal j ON j.guruid = g.id
WHERE g.isactive = true
GROUP BY g.nama, m.nama, g.maxweeklyload
ORDER BY g.nama;";
            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<TeacherLoadRow>(sql);
        }

        public sealed record DashboardCountsRow(int TotalSesi, int SesiHariIni, int Konflik, int GuruAktif);

        public async Task<DashboardCountsRow> GetDashboardCountsAsync(int todayNum)
        {
            const string sql = @"
WITH J AS (
    SELECT guruid, hari, mulai, selesai
    FROM jadwal
),
Pairs AS (
    SELECT CAST(COUNT(*) AS int) AS Cnt
    FROM J a
    JOIN J b
      ON a.guruid = b.guruid
     AND a.hari   = b.hari
     AND (a.mulai < b.selesai AND a.selesai > b.mulai)
     AND (a.mulai <> b.mulai OR a.selesai <> b.selesai)
)
SELECT 
    CAST((SELECT COUNT(*) FROM jadwal) AS int) AS TotalSesi,
    CAST((SELECT COUNT(*) FROM jadwal WHERE hari = @today) AS int) AS SesiHariIni,
    COALESCE((SELECT Cnt FROM Pairs LIMIT 1), 0) AS Konflik,
    CAST((SELECT COUNT(*) FROM guru WHERE isactive = true) AS int) AS GuruAktif;";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QuerySingleAsync<DashboardCountsRow>(sql, new { today = todayNum });
        }

        public async Task<IEnumerable<int>> GetLockedMapelIdsForGuruAsync(int guruId)
        {
            const string sql = @"
SELECT DISTINCT m.id
FROM jadwal j
JOIN mapel m
    ON  LOWER(LTRIM(RTRIM(j.mapel))) = LOWER(LTRIM(RTRIM(m.nama)))
WHERE j.guruid = @guruId;
";

            await using var db = Conn();
            await db.OpenAsync();
            return await db.QueryAsync<int>(sql, new { guruId });
        }

        public async Task<bool> IsEmailExistsAsync(string email, int? excludeId = null)
        {
            string sql = @"
        SELECT COUNT(1) 
        FROM guru 
        WHERE email = @email";

            if (excludeId.HasValue)
            {
                sql += " AND id <> @excludeId";
            }

            await using var db = Conn();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { email, excludeId }) > 0;
        }

        public async Task<bool> IsnipExistsAsync(string nip, int? excludeId = null)
        {
            string sql = "SELECT COUNT(1) FROM guru WHERE nip = @nip";

            if (excludeId.HasValue)
            {
                sql += " AND id <> @excludeId";
            }

            await using var db = Conn();
            await db.OpenAsync();
            var count = await db.ExecuteScalarAsync<int>(sql, new { nip, excludeId });
            return count > 0;
        }

        public async Task<bool> HasClassScheduleConflictAsync(
    int kelasId, int hari, TimeSpan mulai, TimeSpan selesai, int? excludeId = null)
        {
            const string sql = @"
SELECT 1
FROM jadwal j
WHERE j.kelasid = @kelasId
  AND j.hari    = @hari
  AND (@mulai   < j.selesai AND @selesai > j.mulai)
  AND (@excludeId IS NULL OR j.id <> @excludeId);";

            await using var db = Conn();
            await db.OpenAsync();
            var exists = await db.ExecuteScalarAsync<int?>(
                sql, new { kelasId, hari, mulai, selesai, excludeId });

            return exists.HasValue;
        }
    }
}