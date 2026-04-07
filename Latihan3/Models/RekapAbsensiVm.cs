namespace Latihan3.Models
{
    public class RekapAbsensiVm
    {
        public string namaguru { get; set; }
        public string nip { get; set; }
        public int Hadir { get; set; }
        public int Sakit { get; set; }
        public int Izin { get; set; }
        public int Total { get; set; }
    }
}