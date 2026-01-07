namespace Latihan1.Models
{
    public class AdminDashboardViewModel
    {
        public List<SessionVM> UpcomingSessions { get; set; } = new();
        public List<TeacherVM> Teachers { get; set; } = new();
        public DashboardStatsVM Stats { get; set; } = new();
    }

    public class SessionVM
    {
        public int Id { get; set; }
        public DateTime Tanggal { get; set; }
        public string Mulai { get; set; } = "";
        public string Selesai { get; set; } = "";
        public string Guru { get; set; } = "";
        public string Mapel { get; set; } = "";
        public string Kelas { get; set; } = "";
        public string Ruang { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public partial class TeacherVM
    {
        public string Nama { get; set; } = "";
        public string Mapel { get; set; } = "";
        public int Load { get; set; }
        public int MaxLoad { get; set; }
    }

    public class DashboardStatsVM
    {
        public int TotalSesiMingguIni { get; set; }
        public int SesiBerjalanHariIni { get; set; }
        public int KonflikTerbuka { get; set; }
        public int GuruAktif { get; set; }
    }
}
