using Dapper;
using Latihan1.Models;
using Microsoft.Data.SqlClient;
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
        private SqlConnection Conn() => new SqlConnection(_cs);

        // =======================
        // ======== USERS ========
        // =======================
        public record UserRow(int Id, string Username, string PasswordHash, string Role, int? GuruId);

        public async Task<UserRow?> GetUserByUsernameAsync(string username)
        {
            const string sql = @"
SELECT Id, Username, PasswordHash, Role, GuruId
FROM dbo.Users
WHERE Username = @username;";
            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<UserRow>(sql, new { username });
        }

        public async Task<int?> GetUserIdByUsernameAsync(string username)
        {
            const string sql = "SELECT Id FROM dbo.Users WHERE Username = @username;";
            using var db = Conn();
            return await db.ExecuteScalarAsync<int?>(sql, new { username });
        }

        public async Task<int> CreateUserAsync(string username, string passwordHash, string role, int? guruId)
        {
            const string sql = @"
INSERT INTO dbo.Users (Username, PasswordHash, Role, GuruId)
VALUES (@username, @passwordHash, @role, @guruId);";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { username, passwordHash, role, guruId });
        }

        public async Task<int> UpdateUserGuruIdAsync(int userId, int? guruId)
        {
            const string sql = "UPDATE dbo.Users SET GuruId = @guruId WHERE Id = @userId;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { userId, guruId });
        }

        public async Task<bool> GuruExistsAsync(int id)
        {
            const string sql = "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Guru WHERE Id=@id) THEN 1 ELSE 0 END";
            using var db = Conn();
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
SELECT ISNULL(
    MAX(TRY_CONVERT(int, SUBSTRING(Kode, 3, 10))),
    0
)
FROM dbo.Mapel
WHERE Kode LIKE 'MP%';";

            using var db = Conn();
            var lastNumber = await db.ExecuteScalarAsync<int>(sql);
            var nextNumber = lastNumber + 1;

            return "MP" + nextNumber.ToString("D3");
        }


        public async Task<IEnumerable<MapelModel>> GetAllMapelAsync()
        {
            const string sql = "SELECT Id, Kode, Nama FROM dbo.Mapel ORDER BY Nama;";
            using var db = Conn();
            return await db.QueryAsync<MapelModel>(sql);
        }

        public async Task<MapelModel?> GetMapelByIdAsync(int id)
        {
            const string sql = "SELECT Id, Kode, Nama FROM dbo.Mapel WHERE Id=@id;";
            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<MapelModel>(sql, new { id });
        }

        public async Task<int> CreateMapelAsync(MapelModel m)
        {
            const string sql = @"
INSERT INTO dbo.Mapel (Kode, Nama) VALUES (@Kode, @Nama);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            using var db = Conn();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> UpdateMapelAsync(MapelModel m)
        {
            const string sql = "UPDATE dbo.Mapel SET Kode=@Kode, Nama=@Nama WHERE Id=@Id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, m);
        }

        public async Task<int> DeleteMapelAsync(int id)
        {
            const string sql = "DELETE FROM dbo.Mapel WHERE Id=@id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { id });
        }


        // =======================
        // ========= GURU ========
        // =======================
        public async Task<GuruModel?> GetGuruByIdAsync(int id)
        {
            const string sql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'') AS NIP,
    g.Email,
    -- Menghitung total jam dari tabel Jadwal (durasi / 60)
    ISNULL((
        SELECT SUM(DATEDIFF(MINUTE, j.Mulai, j.Selesai)) / 60
        FROM dbo.Jadwal j 
        WHERE j.GuruId = g.Id
    ), 0) AS JamMengajar, 
    ISNULL(g.IsActive, 1) AS IsActive,
    ISNULL(g.MaxWeeklyLoad, 24) AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad, 6) AS MaxDailyLoad,
    -- MaxConsecutiveSlots dihapus dari SELECT karena tidak digunakan lagi
    g.MapelId,
    g.QRCodeBase64,
    m.Nama AS MapelNama,
    g.CreatedAt
FROM dbo.Guru g
JOIN dbo.Mapel m ON m.Id = g.MapelId
WHERE g.Id = @id;";

            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<GuruModel>(sql, new { id });
        }

        public async Task<bool> GuruHasJadwalAsync(int guruId)
        {
            const string sql = @"
SELECT CASE WHEN EXISTS(
    SELECT 1 FROM dbo.Jadwal WHERE GuruId = @guruId
) THEN 1 ELSE 0 END;";
            using var db = Conn();
            return await db.ExecuteScalarAsync<int>(sql, new { guruId }) == 1;
        }

        public Task<GuruModel?> GetGuruForEditAsync(int id) => GetGuruByIdAsync(id);

        public async Task<IEnumerable<GuruModel>> SearchGuruAsync(string? q)
        {
            // Update: Menambahkan g.QRCodeBase64
            const string sql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'') AS NIP,
    g.Email,
    ISNULL(g.JamMengajar,0) AS JamMengajar,
    ISNULL(g.IsActive,1)    AS IsActive,
    ISNULL(g.MaxWeeklyLoad,24)      AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad,6)        AS MaxDailyLoad,
    ISNULL(g.MaxConsecutiveSlots,3) AS MaxConsecutiveSlots,
    g.MapelId,
    g.QRCodeBase64,
    STRING_AGG(m.Nama, ', ') WITHIN GROUP (ORDER BY m.Nama) AS MapelNama,
    g.CreatedAt
FROM dbo.Guru g
LEFT JOIN dbo.GuruMapel gm ON gm.GuruId = g.Id
LEFT JOIN dbo.Mapel     m  ON m.Id      = gm.MapelId
WHERE (@q IS NULL OR @q = '')
   OR (g.Nama  LIKE '%'+@q+'%'
    OR g.NIP   LIKE '%'+@q+'%'
    OR m.Nama  LIKE '%'+@q+'%'
    OR g.Email LIKE '%'+@q+'%')
GROUP BY
    g.Id,
    g.Nama,
    g.NIP,
    g.Email,
    g.JamMengajar,
    g.IsActive,
    g.MaxWeeklyLoad,
    g.MaxDailyLoad,
    g.MaxConsecutiveSlots,
    g.MapelId,
    g.QRCodeBase64,
    g.CreatedAt
ORDER BY g.Nama;";
            using var db = Conn();
            return await db.QueryAsync<GuruModel>(sql, new { q });
        }

        public async Task<(IEnumerable<GuruModel> Items, int Total)> SearchGuruPagedAsync(string? q, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            int skip = (page - 1) * pageSize;

            const string countSql = @"
SELECT COUNT(DISTINCT g.Id)
FROM dbo.Guru g
LEFT JOIN dbo.GuruMapel gm ON gm.GuruId = g.Id
LEFT JOIN dbo.Mapel     m  ON m.Id      = gm.MapelId
WHERE (@q IS NULL OR @q = '')
   OR (g.Nama  LIKE '%'+@q+'%'
    OR g.NIP   LIKE '%'+@q+'%'
    OR m.Nama  LIKE '%'+@q+'%'
    OR g.Email LIKE '%'+@q+'%');";

            // Update: Menambahkan g.QRCodeBase64 dan GROUP BY g.QRCodeBase64
            const string dataSql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'') AS NIP,
    g.Email,
    -- HITUNG TOTAL JAM DARI TABEL JADWAL --
    ISNULL((
        SELECT SUM(DATEDIFF(MINUTE, j.Mulai, j.Selesai)) / 60
        FROM dbo.Jadwal j 
        WHERE j.GuruId = g.Id
    ), 0) AS JamMengajar, 
    ISNULL(g.IsActive,1)    AS IsActive,
    ISNULL(g.MaxWeeklyLoad,24)      AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad,6)        AS MaxDailyLoad,
    ISNULL(g.MaxConsecutiveSlots,3) AS MaxConsecutiveSlots,
    g.MapelId,
    g.QRCodeBase64,
    STRING_AGG(m.Nama, ', ') WITHIN GROUP (ORDER BY m.Nama) AS MapelNama,
    g.CreatedAt
FROM dbo.Guru g
LEFT JOIN dbo.GuruMapel gm ON gm.GuruId = g.Id
LEFT JOIN dbo.Mapel      m  ON m.Id      = gm.MapelId
WHERE (@q IS NULL OR @q = '')
   OR (g.Nama  LIKE '%'+@q+'%'
    OR g.NIP   LIKE '%'+@q+'%'
    OR m.Nama  LIKE '%'+@q+'%'
    OR g.Email LIKE '%'+@q+'%')
GROUP BY
    g.Id, g.Nama, g.NIP, g.Email, g.IsActive, g.MaxWeeklyLoad, 
    g.MaxDailyLoad, g.MaxConsecutiveSlots, g.MapelId, g.QRCodeBase64, g.CreatedAt
ORDER BY g.Nama
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";

            using var db = Conn();
            var total = await db.ExecuteScalarAsync<int>(countSql, new { q });
            var items = await db.QueryAsync<GuruModel>(dataSql, new { q, skip, take = pageSize });
            return (items, total);
        }

        public async Task<int> CreateGuruAsync(GuruModel m)
        {
            // Update: Menambahkan QRCodeBase64 ke Insert
            const string sql = @"
INSERT INTO dbo.Guru
    (Nama, NIP, Email, JamMengajar, IsActive,
     MaxWeeklyLoad, MaxDailyLoad, MaxConsecutiveSlots, MapelId, QRCodeBase64, CreatedAt)
VALUES
    (@Nama, @NIP, @Email, @JamMengajar, @IsActive,
     @MaxWeeklyLoad, @MaxDailyLoad, @MaxConsecutiveSlots, @MapelId, @QRCodeBase64, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);";
            using var db = Conn();
            return await db.ExecuteScalarAsync<int>(sql, m);
        }

        public async Task<int> UpdateGuruAsync(GuruModel m)
        {
            // Update: Menambahkan QRCodeBase64 ke Update
            const string sql = @"
UPDATE dbo.Guru
SET Nama=@Nama,
    NIP=@NIP,
    Email=@Email,
    IsActive=@IsActive,
    MaxWeeklyLoad=@MaxWeeklyLoad,
    MaxDailyLoad=@MaxDailyLoad,
    MapelId=@MapelId,
    QRCodeBase64=@QRCodeBase64
WHERE Id=@Id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, m);
        }

        public async Task<int> DeleteGuruAsync(int id)
        {
            const string sql = "DELETE FROM dbo.Guru WHERE Id=@id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { id });
        }

        public async Task<int> DeleteGuruAndDetachUsersAsync(int id, bool deleteUsers = true)
        {
            using var db = Conn(); // Asumsi Conn() mengembalikan SqlConnection baru
            await db.OpenAsync();

            // Mulai Transaksi
            using var tx = await db.BeginTransactionAsync();

            try
            {
                // 1. HAPUS RELASI MAPEL TERLEBIH DAHULU (Di dalam transaksi)
                // Pastikan nama tabel relasi Anda benar (misal: GuruMapel) dan kolomnya (GuruId)
                const string delMapel = "DELETE FROM dbo.GuruMapel WHERE GuruId = @id;";
                await db.ExecuteAsync(delMapel, new { id }, transaction: (IDbTransaction)tx);

                // 2. PROSES USER (Hapus atau Set Null)
                if (deleteUsers)
                {
                    const string delUsers = "DELETE FROM dbo.Users WHERE GuruId = @id;";
                    await db.ExecuteAsync(delUsers, new { id }, transaction: (IDbTransaction)tx);
                }
                else
                {
                    const string nullUsers = "UPDATE dbo.Users SET GuruId = NULL WHERE GuruId = @id;";
                    await db.ExecuteAsync(nullUsers, new { id }, transaction: (IDbTransaction)tx);
                }

                // 3. HAPUS GURU
                const string delGuru = "DELETE FROM dbo.Guru WHERE Id = @id;";
                var affected = await db.ExecuteAsync(delGuru, new { id }, transaction: (IDbTransaction)tx);

                // Jika sampai sini tidak ada error, Commit semua perubahan
                await tx.CommitAsync();
                return affected;
            }
            catch
            {
                // Jika ada error di langkah manapun, batalkan SEMUANYA (termasuk hapus mapel)
                await tx.RollbackAsync();
                throw; // Lempar error ke Controller
            }
        }

        // =======================================
        // === RELASI GURU – MAPEL (banyak2) ====
        // =======================================

        public async Task<IEnumerable<int>> GetMapelIdsForGuruAsync(int guruId)
        {
            const string sql = @"SELECT MapelId FROM dbo.GuruMapel WHERE GuruId = @guruId;";
            using var db = Conn();
            return await db.QueryAsync<int>(sql, new { guruId });
        }

        public async Task SaveGuruMapelAsync(int guruId, int[] mapelIds)
        {
            using var db = Conn();
            await db.OpenAsync();
            using var tx = await db.BeginTransactionAsync();

            try
            {
                const string delSql = @"DELETE FROM dbo.GuruMapel WHERE GuruId = @guruId;";
                await db.ExecuteAsync(delSql, new { guruId }, transaction: (IDbTransaction)tx);

                if (mapelIds != null && mapelIds.Length > 0)
                {
                    const string insSql = @"INSERT INTO dbo.GuruMapel (GuruId, MapelId) VALUES (@GuruId, @MapelId);";

                    var param = mapelIds
                        .Distinct()
                        .Select(mid => new { GuruId = guruId, MapelId = mid });

                    await db.ExecuteAsync(insSql, param, transaction: (IDbTransaction)tx);
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<int> DeleteGuruMapelAsync(int guruId)
        {
            const string sql = @"DELETE FROM dbo.GuruMapel WHERE GuruId = @guruId;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { guruId });
        }

        // ==============================
        // ===== SUBJECTS (MAPEL) ======
        // ==============================
        public sealed record SubjectCountRow(int MapelId, string Mapel, int Count);

        public async Task<IEnumerable<SubjectCountRow>> GetSubjectsWithCountsAsync()
        {
            const string sql = @"
SELECT m.Id AS MapelId, m.Nama AS Mapel, COUNT(*) AS [Count]
FROM dbo.Guru g
JOIN dbo.Mapel m ON m.Id = g.MapelId
GROUP BY m.Id, m.Nama
ORDER BY m.Nama;";
            using var db = Conn();
            return await db.QueryAsync<SubjectCountRow>(sql);
        }

        public async Task<IEnumerable<string>> GetDistinctMapelAsync()
        {
            const string sql = @"SELECT Nama FROM dbo.Mapel ORDER BY Nama;";
            using var db = Conn();
            return await db.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<GuruModel>> GetTeachersBySubjectIdAsync(int mapelId)
        {
            // Update: Menambahkan QRCodeBase64
            const string sql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'') AS NIP,
    g.Email,
    ISNULL(g.JamMengajar,0) AS JamMengajar,
    ISNULL(g.IsActive,1)    AS IsActive,
    ISNULL(g.MaxWeeklyLoad,24)      AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad,6)        AS MaxDailyLoad,
    ISNULL(g.MaxConsecutiveSlots,3) AS MaxConsecutiveSlots,
    g.MapelId,
    g.QRCodeBase64,
    m.Nama AS MapelNama,
    g.CreatedAt
FROM dbo.Guru g
JOIN dbo.Mapel m ON m.Id = g.MapelId
WHERE g.MapelId = @mapelId
ORDER BY g.Nama;";
            using var db = Conn();
            return await db.QueryAsync<GuruModel>(sql, new { mapelId });
        }

        public async Task<IEnumerable<GuruModel>> GetTeachersBySubjectAsync(string mapelNama)
        {
            // Update: Menambahkan QRCodeBase64
            const string sql = @"
SELECT 
    g.Id,
    g.Nama,
    ISNULL(g.NIP,'') AS NIP,
    g.Email,
    ISNULL(g.JamMengajar,0) AS JamMengajar,
    ISNULL(g.IsActive,1)    AS IsActive,
    ISNULL(g.MaxWeeklyLoad,24)      AS MaxWeeklyLoad,
    ISNULL(g.MaxDailyLoad,6)        AS MaxDailyLoad,
    ISNULL(g.MaxConsecutiveSlots,3) AS MaxConsecutiveSlots,
    g.MapelId,
    g.QRCodeBase64,
    m.Nama AS MapelNama,
    g.CreatedAt
FROM dbo.Guru g
JOIN dbo.Mapel m ON m.Id = g.MapelId
WHERE m.Nama = @mapelNama
ORDER BY g.Nama;";
            using var db = Conn();
            return await db.QueryAsync<GuruModel>(sql, new { mapelNama });
        }

        // =======================
        // ========= KELAS =======
        // =======================
        public record KelasRow(int Id, string Tingkat, string Nama, bool IsActive, DateTime CreatedAt);

        public async Task<IEnumerable<string>> GetAllGradesAsync()
        {
            const string sql = @"
SELECT DISTINCT Tingkat
FROM dbo.Kelas
WHERE IsActive = 1
ORDER BY Tingkat;";
            using var db = Conn();
            return await db.QueryAsync<string>(sql);
        }

        public async Task<IEnumerable<KelasRow>> GetKelasByGradeAsync(string tingkat)
        {
            const string sql = @"
SELECT Id, Tingkat, Nama, ISNULL(IsActive,1) AS IsActive, CreatedAt
FROM dbo.Kelas
WHERE Tingkat = @tingkat
ORDER BY Nama;";
            using var db = Conn();
            return await db.QueryAsync<KelasRow>(sql, new { tingkat });
        }

        public async Task<int> InsertKelasAsync(string tingkat, string nama, bool isActive = true)
        {
            const string sql = @"
INSERT INTO dbo.Kelas (Tingkat, Nama, IsActive, CreatedAt)
VALUES (@tingkat, @nama, @isActive, SYSDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS int);";
            using var db = Conn();
            return await db.ExecuteScalarAsync<int>(sql, new { tingkat, nama, isActive });
        }

        public async Task<int> UpdateKelasAsync(int id, string tingkat, string nama, bool isActive)
        {
            const string sql = @"
UPDATE dbo.Kelas
SET Tingkat = @tingkat,
    Nama    = @nama,
    IsActive = @isActive
WHERE Id = @id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { id, tingkat, nama, isActive });
        }

        public async Task<int> DeleteKelasAsync(int id)
        {
            const string sql = @"DELETE FROM dbo.Kelas WHERE Id = @id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { id });
        }

        public async Task<IEnumerable<KelasRow>> GetKelasListAsync()
        {
            const string sql = @"
SELECT Id, Tingkat, Nama, ISNULL(IsActive,1) AS IsActive, CreatedAt
FROM dbo.Kelas
ORDER BY Tingkat, Nama;";
            using var db = Conn();
            return await db.QueryAsync<KelasRow>(sql);
        }

        public record KelasGroupRow(string Tingkat, int Count);
        public async Task<IEnumerable<KelasGroupRow>> GetKelasGroupsAsync()
        {
            const string sql = @"
SELECT Tingkat, COUNT(*) AS [Count]
FROM dbo.Kelas
GROUP BY Tingkat
ORDER BY Tingkat;";
            using var db = Conn();
            return await db.QueryAsync<KelasGroupRow>(sql);
        }

        public Task<IEnumerable<KelasRow>> GetKelasByTingkatAsync(string tingkat)
            => GetKelasByGradeAsync(tingkat);

        public async Task<KelasRow?> GetKelasByIdAsync(int id)
        {
            const string sql = @"
SELECT Id, Tingkat, Nama, ISNULL(IsActive,1) AS IsActive, CreatedAt
FROM dbo.Kelas
WHERE Id = @id;";
            using var db = Conn();
            return await db.QueryFirstOrDefaultAsync<KelasRow>(sql, new { id });
        }

        public Task<int> CreateKelasAsync(KelasModel m)
            => InsertKelasAsync(m.Tingkat!, m.Nama!, m.IsActive);

        public Task<int> UpdateKelasAsync(KelasModel m)
            => UpdateKelasAsync(m.Id, m.Tingkat!, m.Nama!, m.IsActive);

        public async Task<int> UpdateKelasAsync(int id, string nama, bool isActive)
        {
            var tingkat = await GetKelasTingkatAsyncInternal(id);
            return await UpdateKelasAsync(id, tingkat, nama, isActive);
        }

        private async Task<string> GetKelasTingkatAsyncInternal(int id)
        {
            const string sql = "SELECT TOP(1) Tingkat FROM dbo.Kelas WHERE Id=@id;";
            using var db = Conn();
            return await db.ExecuteScalarAsync<string>(sql, new { id }) ?? "X";
        }

        public Task<int> CreateKelasAsync(string tingkat, string nama, bool isActive = true)
            => InsertKelasAsync(tingkat, nama, isActive);

        public Task<int> CreateKelasAsync(string tingkat)
            => InsertKelasAsync(tingkat, $"{tingkat} 1", true);

        // ==========================
        // ========= JADWAL =========
        // ==========================
        // POCO untuk Dapper
        public class JadwalRow
        {
            public int Id { get; set; }
            public int GuruId { get; set; }
            public string GuruNama { get; set; } = "";
            public string Mapel { get; set; } = "";  // dari tabel Jadwal (string)
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
FROM dbo.Jadwal j
WHERE j.GuruId = @GuruId
  AND j.Hari   = @Hari
  AND (@Mulai  < j.Selesai AND @Selesai > j.Mulai)
  AND (@ExcludeId IS NULL OR j.Id <> @ExcludeId);";

            using var db = Conn();
            var exists = await db.ExecuteScalarAsync<int?>(
                sql, new { GuruId = guruId, Hari = hari, Mulai = mulai, Selesai = selesai, ExcludeId = excludeId });

            return exists.HasValue;
        }

        public async Task<int> CreateJadwalAsync(
            int guruId, string mapel, int hari, TimeSpan mulai, TimeSpan selesai, int kelasId, string? ruangan)
        {
            const string sql = @"
INSERT INTO dbo.Jadwal (GuruId, Mapel, Hari, Mulai, Selesai, KelasId, Ruangan)
VALUES (@GuruId, @Mapel, @Hari, @Mulai, @Selesai, @KelasId, @Ruangan);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var db = Conn();
            return await db.ExecuteScalarAsync<int>(sql, new
            {
                GuruId = guruId,
                Mapel = mapel, // tetap string di tabel Jadwal
                Hari = hari,
                Mulai = mulai,
                Selesai = selesai,
                KelasId = kelasId,
                Ruangan = ruangan
            });
        }

        //EDIT JADWAL
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
UPDATE dbo.Jadwal
SET GuruId = @GuruId,
    Mapel  = @Mapel,
    Hari   = @Hari,
    Mulai  = @Mulai,
    Selesai = @Selesai,
    KelasId = @KelasId,
    Ruangan = @Ruangan
WHERE Id = @Id;";

            using var db = Conn();
            return await db.ExecuteAsync(sql, new
            {
                Id = id,
                GuruId = guruId,
                Mapel = mapel,
                Hari = hari,
                Mulai = mulai,
                Selesai = selesai,
                KelasId = kelasId,
                Ruangan = ruangan
            });
        }

        public async Task<IEnumerable<JadwalRow>> ListJadwalAsync(int? hari = null)
        {
            const string sql = @"
SELECT  j.Id,
        j.GuruId,
        ISNULL(g.Nama,'(Guru tidak ada)')                   AS GuruNama,
        COALESCE(m.Nama, j.Mapel, '(Tanpa mapel)')         AS Mapel, 
        CAST(j.Hari AS int)                                 AS Hari,
        j.Mulai,
        j.Selesai,
        j.KelasId,
        ISNULL(k.Nama,'(Kelas tidak ada)')                  AS KelasNama,
        j.Ruangan
FROM dbo.Jadwal j
LEFT JOIN dbo.Guru  g ON g.Id = j.GuruId
LEFT JOIN dbo.Kelas k ON k.Id = j.KelasId
LEFT JOIN dbo.Mapel m ON m.Id = g.MapelId           
WHERE (@hari IS NULL OR j.Hari = @hari)
ORDER BY j.Hari, j.Mulai;";
            using var db = Conn();
            return await db.QueryAsync<JadwalRow>(sql, new { hari });
        }

        public async Task<int> DeleteJadwalAsync(int id)
        {
            const string sql = @"DELETE FROM dbo.Jadwal WHERE Id = @id;";
            using var db = Conn();
            return await db.ExecuteAsync(sql, new { id });
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

        // === GURU + LOAD (pakai JOIN Mapel) ===
        public record GuruLoadRow(int Id, string Guru, string Mapel, int MaxWeeklyLoad, int CurrentLoad);

        public async Task<IEnumerable<GuruLoadRow>> GetGuruByMapelWithLoadAsync(string mapelNama)
        {
            const string sql = @"
SELECT 
    g.Id,
    g.Nama AS Guru,
    @mapelNama AS Mapel,                                   
    ISNULL(g.MaxWeeklyLoad,24) AS MaxWeeklyLoad,
    ISNULL(SUM(DATEDIFF(MINUTE, j.Mulai, j.Selesai)), 0) AS CurrentLoad
FROM dbo.Guru g
JOIN dbo.GuruMapel gm ON gm.GuruId = g.Id         
JOIN dbo.Mapel m     ON m.Id      = gm.MapelId
LEFT JOIN dbo.Jadwal j ON j.GuruId = g.Id
WHERE m.Nama = @mapelNama                                  
GROUP BY g.Id, g.Nama, g.MaxWeeklyLoad
ORDER BY g.Nama;";

            using var db = Conn();
            return await db.QueryAsync<GuruLoadRow>(sql, new { mapelNama });
        }

        // Aliases konflik waktu (legacy + baru)
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
SELECT j.Id,
       j.GuruId,
       ISNULL(g.Nama,'(Guru tidak ada)') AS GuruNama,
       ISNULL(j.Mapel,'(Tanpa mapel)')   AS Mapel,
       CAST(j.Hari AS int)               AS Hari,
       j.Mulai,
       j.Selesai,
       j.KelasId,
       ISNULL(k.Nama,'(Kelas tidak ada)') AS KelasNama,
       j.Ruangan
FROM dbo.Jadwal j
LEFT JOIN dbo.Guru  g ON g.Id = j.GuruId
LEFT JOIN dbo.Kelas k ON k.Id = j.KelasId
WHERE j.KelasId = @kelasId
ORDER BY j.Hari, j.Mulai;";
            using var db = Conn();
            return await db.QueryAsync<JadwalRow>(sql, new { kelasId });
        }

        public async Task<IEnumerable<Latihan1.Models.MapelListItemVm>> GetMapelWithCountsAsync()
        {
            const string sql = @"
SELECT m.Id, m.Kode, m.Nama, COUNT(g.Id) AS [Count]
FROM dbo.Mapel m
LEFT JOIN dbo.Guru g ON g.MapelId = m.Id
GROUP BY m.Id, m.Kode, m.Nama
ORDER BY m.Nama;";
            using var db = Conn();
            return await db.QueryAsync<Latihan1.Models.MapelListItemVm>(sql);
        }

        // ===== DASHBOARD HELPERS =====
        public sealed record TeacherLoadRow(string Nama, string Mapel, int LoadMinutes, int MaxWeeklyLoad);

        public async Task<IEnumerable<TeacherLoadRow>> GetTeacherLoadsAsync()
        {
            const string sql = @"
SELECT 
    g.Nama,
    m.Nama AS Mapel,
    ISNULL(SUM(DATEDIFF(MINUTE, j.Mulai, j.Selesai)), 0) AS LoadMinutes,
    ISNULL(g.MaxWeeklyLoad, 24) AS MaxWeeklyLoad
FROM dbo.Guru g
JOIN dbo.Mapel m ON m.Id = g.MapelId
LEFT JOIN dbo.Jadwal j ON j.GuruId = g.Id
WHERE g.IsActive = 1
GROUP BY g.Nama, m.Nama, g.MaxWeeklyLoad
ORDER BY g.Nama;";
            using var db = Conn();
            return await db.QueryAsync<TeacherLoadRow>(sql);
        }

        public sealed record DashboardCountsRow(int TotalSesi, int SesiHariIni, int Konflik, int GuruAktif);

        public async Task<DashboardCountsRow> GetDashboardCountsAsync(int todayNum)
        {
            const string sql = @"
DECLARE @TotalSesi   int = (SELECT COUNT(*) FROM dbo.Jadwal);
DECLARE @SesiHariIni int = (SELECT COUNT(*) FROM dbo.Jadwal WHERE Hari = @today);

;WITH J AS (
    SELECT GuruId, Hari, Mulai, Selesai
    FROM dbo.Jadwal
),
Pairs AS (
    SELECT COUNT(*) AS Cnt
    FROM J a
    JOIN J b
      ON a.GuruId = b.GuruId
     AND a.Hari   = b.Hari
     AND (a.Mulai < b.Selesai AND a.Selesai > b.Mulai)
     AND (a.Mulai <> b.Mulai OR a.Selesai <> b.Selesai)
)
SELECT 
    @TotalSesi                              AS TotalSesi,
    @SesiHariIni                            AS SesiHariIni,
    ISNULL((SELECT TOP 1 Cnt FROM Pairs),0) AS Konflik,
    (SELECT COUNT(*) FROM dbo.Guru WHERE IsActive = 1) AS GuruAktif;";
            using var db = Conn();
            return await db.QuerySingleAsync<DashboardCountsRow>(sql, new { today = todayNum });
        }

        public async Task<IEnumerable<int>> GetLockedMapelIdsForGuruAsync(int guruId)
        {
            const string sql = @"
SELECT DISTINCT m.Id
FROM dbo.Jadwal j
JOIN dbo.Mapel m
    ON  LOWER(LTRIM(RTRIM(j.Mapel))) = LOWER(LTRIM(RTRIM(m.Nama)))    -- samakan nama mapel (case-insensitive + trim)
WHERE j.GuruId = @GuruId;
";

            using var conn = Conn();
            return await conn.QueryAsync<int>(sql, new { GuruId = guruId });
        }

        public async Task<bool> IsEmailExistsAsync(string email, int? excludeId = null)
        {
            // Gunakan 'string' atau 'var', JANGAN gunakan 'const'
            string sql = @"
        SELECT COUNT(1) 
        FROM dbo.Guru 
        WHERE Email = @email";

            // Tambahkan kondisi secara dinamis
            if (excludeId.HasValue)
            {
                sql += " AND Id <> @excludeId";
            }

            using var db = Conn();
            // Dapper akan otomatis mencocokkan parameter meskipun excludeId bernilai null
            return await db.ExecuteScalarAsync<int>(sql, new { email, excludeId }) > 0;
        }
        public async Task<bool> IsNipExistsAsync(string nip, int? excludeId = null)
        {
            // Cek apakah NIP sudah ada, jika excludeId diisi (saat Edit), abaikan ID tersebut
            string sql = "SELECT COUNT(1) FROM dbo.Guru WHERE NIP = @nip";

            if (excludeId.HasValue)
            {
                sql += " AND Id <> @excludeId";
            }

            using var db = Conn();
            var count = await db.ExecuteScalarAsync<int>(sql, new { nip, excludeId });
            return count > 0;
        }

        public async Task<bool> HasClassScheduleConflictAsync(
    int kelasId, int hari, TimeSpan mulai, TimeSpan selesai, int? excludeId = null)
        {
            const string sql = @"
SELECT 1
FROM dbo.Jadwal j
WHERE j.KelasId = @KelasId
  AND j.Hari    = @Hari
  AND (@Mulai   < j.Selesai AND @Selesai > j.Mulai)
  AND (@ExcludeId IS NULL OR j.Id <> @ExcludeId);";

            using var db = Conn();
            var exists = await db.ExecuteScalarAsync<int?>(
                sql, new { KelasId = kelasId, Hari = hari, Mulai = mulai, Selesai = selesai, ExcludeId = excludeId });

            return exists.HasValue;
        }
    }
}