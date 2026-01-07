namespace Latihan1.Models
{
    public class KelasModel
    {
        public int Id { get; set; }
        public string Tingkat { get; set; } = "X";   // X, XI, XII
        public string Nama { get; set; } = "";       // X1, X2, ...
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
    }

    public class KelasListVm
    {
        public string? Tingkat { get; set; }                 // filter tingkat (null = grid tingkat)
        public IEnumerable<KelasModel> Items { get; set; } = Enumerable.Empty<KelasModel>();
    }

    public class KelasGroupVm
    {
        public string Tingkat { get; set; } = "";
        public int Count { get; set; }
    }
    public class KelasJadwalVm
    {
        public KelasModel Kelas { get; set; } = new();
        public IEnumerable<Latihan1.Services.DapperDb.JadwalRow> Items { get; set; }
            = Enumerable.Empty<Latihan1.Services.DapperDb.JadwalRow>();
    }

}
