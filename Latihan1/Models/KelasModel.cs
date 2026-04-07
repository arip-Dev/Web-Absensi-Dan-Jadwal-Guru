namespace Latihan1.Models
{
    public class kelasModel
    {
        public int id { get; set; }
        public string tingkat { get; set; } = "X";   // X, XI, XII
        public string nama { get; set; } = "";       // X1, X2, ...
        public bool isactive { get; set; } = true;
        public DateTime? createdat { get; set; }
    }

    public class kelasListVm
    {
        public string? tingkat { get; set; }                 // filter tingkat (null = grid tingkat)
        public IEnumerable<kelasModel> Items { get; set; } = Enumerable.Empty<kelasModel>();
    }

    public class kelasGroupVm
    {
        public string tingkat { get; set; } = "";
        public int Count { get; set; }
    }
    public class kelasjadwalVm
    {
        public kelasModel kelas { get; set; } = new();
        public IEnumerable<Latihan1.Services.DapperDb.JadwalRow> Items { get; set; }
            = Enumerable.Empty<Latihan1.Services.DapperDb.JadwalRow>();
    }

}
