using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Latihan3.Models;
using Microsoft.Extensions.Configuration;

namespace Latihan3.Services
{
    public class DapperDb3
    {
        private readonly string? _connectionString;

        public DapperDb3(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Connection string tidak ditemukan.");
            }
        }

        // Properti koneksi utama (digunakan di seluruh method)
        private IDbConnection Connection => new SqlConnection(_connectionString!);

        // ================= FITUR DAFTAR ABSENSI (PAGED) =================

        public async Task<IEnumerable<AbsensiGuru>> GetPagedAbsensiAsync(int page, int pageSize)
        {
            int offset = (page - 1) * pageSize;

            const string sql = @"
                SELECT a.*, g.Nama AS NamaGuru, g.NIP 
                FROM dbo.AbsensiGuru a
                JOIN dbo.Guru g ON g.Id = a.Id
                ORDER BY a.Tanggal DESC 
                OFFSET @offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";

            using var db = Connection;
            return await db.QueryAsync<AbsensiGuru>(sql, new { offset, pageSize });
        }

        public async Task<int> GetTotalAbsensiCountAsync()
        {
            const string sql = "SELECT COUNT(*) FROM dbo.AbsensiGuru";
            using var db = Connection;
            return await db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<AbsensiGuru?> GetAbsensiByIdAsync(int id)
        {
            using var db = Connection;
            string sql = @"
                SELECT a.AbsensiId, a.Id, a.Tanggal, a.Status, a.Keterangan,
                       g.Nama AS NamaGuru, g.NIP
                FROM AbsensiGuru a
                LEFT JOIN Guru g ON g.Id = a.Id
                WHERE a.AbsensiId = @id";

            return await db.QueryFirstOrDefaultAsync<AbsensiGuru>(sql, new { id });
        }

        // ================= FITUR REKAPITULASI (PAGED) =================

        public async Task<IEnumerable<RekapAbsensiVm>> GetPagedRekapAbsensiAsync(int bulan, int tahun, int page, int pageSize, string? keyword = null)
        {
            int offset = (page - 1) * pageSize;
            string search = $"%{keyword}%";

            const string sql = @"
                SELECT 
                    g.Nama AS NamaGuru, 
                    g.NIP,
                    SUM(CASE WHEN a.Status = 'Hadir' THEN 1 ELSE 0 END) AS Hadir,
                    SUM(CASE WHEN a.Status = 'Sakit' THEN 1 ELSE 0 END) AS Sakit,
                    SUM(CASE WHEN a.Status = 'Izin' THEN 1 ELSE 0 END) AS Izin,
                    COUNT(a.AbsensiId) AS Total
                FROM dbo.Guru g
                LEFT JOIN dbo.AbsensiGuru a ON g.Id = a.Id 
                    AND YEAR(a.Tanggal) = @targetTahun
                    AND (@targetBulan = 0 OR MONTH(a.Tanggal) = @targetBulan)
                WHERE (@keyword IS NULL OR g.Nama LIKE @search OR g.NIP LIKE @search)
                GROUP BY g.Nama, g.NIP
                ORDER BY g.Nama ASC
                OFFSET @offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";

            using var db = Connection;
            return await db.QueryAsync<RekapAbsensiVm>(sql, new { targetBulan = bulan, targetTahun = tahun, search, keyword, offset, pageSize });
        }

        public async Task<int> GetTotalGuruCountAsync(string? keyword = null)
        {
            string search = $"%{keyword}%";
            const string sql = "SELECT COUNT(*) FROM dbo.Guru WHERE (@keyword IS NULL OR Nama LIKE @search OR NIP LIKE @search)";
            using var db = Connection;
            return await db.ExecuteScalarAsync<int>(sql, new { keyword, search });
        }

        // ================= OPERATIONS (CRUD) =================

        public async Task<int> InsertAbsensiAsync(AbsensiGuru abs)
        {
            using var db = Connection;
            string sql = @"INSERT INTO AbsensiGuru (Id, Tanggal, Status, Keterangan) 
                           VALUES (@Id, @Tanggal, @Status, @Keterangan);
                           SELECT CAST(SCOPE_IDENTITY() AS INT);";
            return await db.ExecuteScalarAsync<int>(sql, abs);
        }

        public async Task UpdateAbsensiAsync(AbsensiGuru abs)
        {
            using var db = Connection;
            string sql = @"UPDATE AbsensiGuru SET Id=@Id, Tanggal=@Tanggal, Status=@Status, Keterangan=@Keterangan WHERE AbsensiId=@AbsensiId";
            await db.ExecuteAsync(sql, abs);
        }

        public async Task DeleteAbsensiAsync(int id)
        {
            using var db = Connection;
            string sql = "DELETE FROM AbsensiGuru WHERE AbsensiId = @id";
            await db.ExecuteAsync(sql, new { id });
        }

        // ================= SEARCH & SELECT2 HELPERS (YANG BARU) =================

        // 1. Method Search (Mengambil data sedikit demi sedikit / Paging via Select2)
        public async Task<IEnumerable<Guru>> SearchGuruByNameAsync(string keyword)
        {
            // Perbaikan: Gunakan properti 'Connection' yang sudah ada, bukan 'CreateConnection()'
            using var db = Connection;

            // Mengambil 20 data teratas saja agar ringan
            string sql = @"
                SELECT TOP 20 Id, Nama, NIP 
                FROM Guru 
                WHERE Nama LIKE @Search OR NIP LIKE @Search
                ORDER BY Nama";

            return await db.QueryAsync<Guru>(sql, new { Search = $"%{keyword}%" });
        }

        // 2. Method Get By ID (Dipakai saat Validasi Error di Controller)
        public async Task<Guru?> GetGuruByIdAsync(int id)
        {
            using var db = Connection;
            string sql = "SELECT * FROM Guru WHERE Id = @Id";
            return await db.QuerySingleOrDefaultAsync<Guru>(sql, new { Id = id });
        }

        // ================= HELPERS LAIN & AUTH =================

        // Class internal kecil untuk dropdown legacy (opsional, jika masih dipakai di fitur lain)
        public class GuruOption
        {
            public int Id { get; set; }
            public string Nama { get; set; } = "";
        }

        public async Task<IEnumerable<GuruOption>> GetGuruListAsync()
        {
            using var db = Connection;
            string sql = "SELECT Id, Nama FROM Guru ORDER BY Nama";
            return await db.QueryAsync<GuruOption>(sql);
        }

        public async Task<int?> GetGuruIdByNipAsync(string nip)
        {
            using var db = Connection;
            string sql = "SELECT Id FROM Guru WHERE NIP = @nip";
            return await db.ExecuteScalarAsync<int?>(sql, new { nip });
        }

        public async Task<string?> GetGuruNamaByIdAsync(int id)
        {
            using var db = Connection;
            string sql = "SELECT Nama FROM Guru WHERE Id = @id";
            return await db.ExecuteScalarAsync<string?>(sql, new { id });
        }

        public async Task<bool> IsAlreadyPresentAsync(int guruId, DateTime date)
        {
            using var db = Connection;
            string sql = "SELECT COUNT(1) FROM AbsensiGuru WHERE Id = @guruId AND CAST(Tanggal AS DATE) = CAST(@date AS DATE)";
            var count = await db.ExecuteScalarAsync<int>(sql, new { guruId, date });
            return count > 0;
        }

        public async Task<string?> GetPasswordHashByUsernameAsync(string username)
        {
            using var db = Connection;
            string sql = "SELECT PasswordHash FROM Users WHERE Username = @u";
            return await db.QueryFirstOrDefaultAsync<string>(sql, new { u = username });
        }

        public async Task<string?> GetUserRoleAsync(string username)
        {
            using var db = Connection;
            return await db.QueryFirstOrDefaultAsync<string>(
                "SELECT Role FROM Users WHERE Username = @u", new { u = username });
        }
    }
}