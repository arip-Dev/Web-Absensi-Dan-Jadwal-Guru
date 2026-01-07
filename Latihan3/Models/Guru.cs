using System.ComponentModel.DataAnnotations;

namespace Latihan3.Models
{
    public class Guru
    {
        [Key]
        public int Id { get; set; }

        public string? Nama { get; set; }

        public string? NIP { get; set; }

        // Tambahkan properti lain jika ada di tabel database Anda (misal Alamat, NoHP), 
        // tapi untuk fitur ini, 3 di atas sudah cukup.
    }
}