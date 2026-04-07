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
        public int id { get; set; }
        public DateTime tanggal { get; set; }
        public string mulai { get; set; } = "";
        public string selesai { get; set; } = "";
        public string guru { get; set; } = "";
        public string mapel { get; set; } = "";
        public string kelas { get; set; } = "";
        public string Ruang { get; set; } = "";
        public string status { get; set; } = "";
    }

    public partial class TeacherVM
    {
        public string nama { get; set; } = "";
        public string mapel { get; set; } = "";
        public int Load { get; set; }
        public int MaxLoad { get; set; }
    }

    public class DashboardStatsVM
    {
        public int TotalSesiMingguIni { get; set; }
        public int SesiBerjalanhariIni { get; set; }
        public int KonflikTerbuka { get; set; }
        public int guruAktif { get; set; }
    }
}
