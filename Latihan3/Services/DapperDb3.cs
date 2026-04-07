using System.Data;
using Dapper;
using Npgsql;
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

        // PERBAIKAN 1: Gunakan NpgsqlConnection langsung agar OpenAsync bisa digunakan
        private NpgsqlConnection Connection => new NpgsqlConnection(_connectionString!);

        // ================= FITUR DAFTAR ABSENSI (PAGED) =================

        public async Task<IEnumerable<absensiguru>> GetPagedAbsensiAsync(int page, int pageSize)
        {
            int offset = (page - 1) * pageSize;

            const string sql = @"
                SELECT a.*, g.nama AS namaguru, g.nip 
                FROM absensiguru a
                JOIN guru g ON g.id = a.id
                ORDER BY a.tanggal DESC 
                OFFSET @offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";

            await using var db = Connection;
            await db.OpenAsync(); // PERBAIKAN 2: Wajib ada
            return await db.QueryAsync<absensiguru>(sql, new { offset, pageSize });
        }

        public async Task<int> GetTotalAbsensiCountAsync()
        {
            const string sql = "SELECT COUNT(*) FROM absensiguru";
            await using var db = Connection;
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql);
        }

        public async Task<absensiguru?> GetAbsensiByidAsync(int id)
        {
            await using var db = Connection;
            await db.OpenAsync();

            string sql = @"
                SELECT a.absensiid, a.id, a.tanggal, a.status, a.keterangan,
                       g.nama AS namaguru, g.nip
                FROM absensiguru a
                LEFT JOIN guru g ON g.id = a.id
                WHERE a.absensiid = @id";

            return await db.QueryFirstOrDefaultAsync<absensiguru>(sql, new { id });
        }

        // ================= FITUR REKAPITULASI (PAGED) =================

        public async Task<IEnumerable<RekapAbsensiVm>> GetPagedRekapAbsensiAsync(int bulan, int tahun, int page, int pageSize, string? keyword = null)
        {
            int offset = (page - 1) * pageSize;
            string search = $"%{keyword}%";

            // PERBAIKAN 3: Ganti YEAR() dan MONTH() menjadi sintaks PostgreSQL (EXTRACT)
            // Serta gunakan ILIKE untuk pencarian agar tidak case-sensitive
            const string sql = @"
                SELECT 
                    g.nama AS namaguru, 
                    g.nip,
                    SUM(CASE WHEN a.status = 'Hadir' THEN 1 ELSE 0 END) AS Hadir,
                    SUM(CASE WHEN a.status = 'Sakit' THEN 1 ELSE 0 END) AS Sakit,
                    SUM(CASE WHEN a.status = 'Izin' THEN 1 ELSE 0 END) AS Izin,
                    COUNT(a.absensiid) AS Total
                FROM guru g
                LEFT JOIN absensiguru a ON g.id = a.id 
                    AND EXTRACT(YEAR FROM a.tanggal) = @targetTahun
                    AND (@targetBulan = 0 OR EXTRACT(MONTH FROM a.tanggal) = @targetBulan)
                WHERE (@keyword IS NULL OR g.nama ILIKE @search OR g.nip ILIKE @search)
                GROUP BY g.nama, g.nip
                ORDER BY g.nama ASC
                OFFSET @offset ROWS 
                FETCH NEXT @pageSize ROWS ONLY";

            await using var db = Connection;
            await db.OpenAsync();
            return await db.QueryAsync<RekapAbsensiVm>(sql, new { targetBulan = bulan, targetTahun = tahun, search, keyword, offset, pageSize });
        }

        public async Task<int> GetTotalguruCountAsync(string? keyword = null)
        {
            string search = $"%{keyword}%";
            const string sql = "SELECT COUNT(*) FROM guru WHERE (@keyword IS NULL OR nama ILIKE @search OR nip ILIKE @search)";
            await using var db = Connection;
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(sql, new { keyword, search });
        }

        // ================= OPERATIONS (CRUD) =================

        public async Task<int> InsertAbsensiAsync(absensiguru abs)
        {
            await using var db = Connection;
            await db.OpenAsync();
            // PERBAIKAN 4: Menghilangkan titik koma (;) yang salah tempat sebelum RETURNING
            string sql = @"INSERT INTO absensiguru (id, tanggal, status, keterangan) 
                           VALUES (@id, @tanggal, @status, @keterangan)
                           RETURNING absensiid;";
            return await db.ExecuteScalarAsync<int>(sql, abs);
        }

        public async Task UpdateAbsensiAsync(absensiguru abs)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = @"UPDATE absensiguru SET id=@id, tanggal=@tanggal, status=@status, keterangan=@keterangan WHERE absensiid=@absensiid";
            await db.ExecuteScalarAsync<int>(sql, abs);
        }

        public async Task DeleteAbsensiAsync(int id)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "DELETE FROM absensiguru WHERE absensiid = @id";
            await db.ExecuteScalarAsync<int>(sql, new { id });
        }

        // ================= SEARCH & SELECT2 HELPERS (YANG BARU) =================

        // 1. Method Search (Mengambil data sedikit demi sedikit / Paging via Select2)
        public async Task<IEnumerable<guru>> SearchguruByNameAsync(string keyword)
        {
            await using var db = Connection;
            await db.OpenAsync();

            // PERBAIKAN 5: Mengubah TOP 20 menjadi LIMIT 20 (Standar PostgreSQL)
            string sql = @"
                SELECT id, nama, nip 
                FROM guru 
                WHERE nama ILIKE @Search OR nip ILIKE @Search
                ORDER BY nama
                LIMIT 20";

            return await db.QueryAsync<guru>(sql, new { Search = $"%{keyword}%" });
        }

        // 2. Method Get By id (Dipakai saat Validasi Error di Controller)
        public async Task<guru?> GetGuruByIdAsync(int id)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT * FROM guru WHERE id = @id";
            return await db.QuerySingleOrDefaultAsync<guru>(sql, new { id = id });
        }

        // ================= HELPERS LAIN & AUTH =================

        public class GuruOption
        {
            public int Id { get; set; }
            public string Nama { get; set; } = "";
        }

        public async Task<IEnumerable<GuruOption>> GetguruListAsync()
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT id, nama FROM guru ORDER BY nama";
            return await db.QueryAsync<GuruOption>(sql);
        }

        public async Task<int?> GetguruidBynipAsync(string nip)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT id FROM guru WHERE nip = @nip";
            return await db.ExecuteScalarAsync<int?>(sql, new { nip });
        }

        public async Task<string?> GetgurunamaByidAsync(int id)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT nama FROM guru WHERE id = @id";
            return await db.ExecuteScalarAsync<string?>(sql, new { id });
        }

        public async Task<bool> IsAlreadyPresentAsync(int guruid, DateTime date)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT COUNT(1) FROM absensiguru WHERE id = @guruid AND CAST(tanggal AS DATE) = CAST(@date AS DATE)";
            var count = await db.ExecuteScalarAsync<int>(sql, new { guruid, date });
            return count > 0;
        }

        public async Task<string?> GetpasswordhashByUsernameAsync(string username)
        {
            await using var db = Connection;
            await db.OpenAsync();
            string sql = "SELECT passwordhash FROM users WHERE username = @u";
            return await db.QueryFirstOrDefaultAsync<string>(sql, new { u = username });
        }

        public async Task<string?> GetUserRoleAsync(string username)
        {
            await using var db = Connection;
            await db.OpenAsync();
            return await db.QueryFirstOrDefaultAsync<string>(
                "SELECT role FROM users WHERE username = @u", new { u = username });
        }
    }
}