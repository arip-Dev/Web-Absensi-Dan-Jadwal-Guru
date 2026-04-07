using System.ComponentModel.DataAnnotations;

namespace Latihan3.Models
{
    public class guru
    {
        [Key]
        public int id { get; set; }

        public string? nama { get; set; }

        public string? nip { get; set; }

        // Tambahkan properti lain jika ada di tabel database Anda (misal Alamat, NoHP), 
        // tapi untuk fitur ini, 3 di atas sudah cukup.
    }
}