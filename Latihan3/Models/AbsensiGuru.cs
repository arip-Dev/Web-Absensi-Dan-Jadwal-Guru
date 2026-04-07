using System;
using System.ComponentModel.DataAnnotations;

namespace Latihan3.Models
{
    public class absensiguru
    {
        [Key]
        public int absensiid { get; set; }

        [Display(Name = "guru")]
        public int id { get; set; } // Ini guruid (FK)

        [Display(Name = "tanggal")]
        [DataType(DataType.DateTime)]
        public DateTime tanggal { get; set; }

        [StringLength(50)]
        public string? status { get; set; }

        public string? keterangan { get; set; }

        // --- TAMBAHAN (Untuk Menampilkan Data JOIN) ---
        // Properti ini tidak disimpan ke tabel Absensi, tapi dibaca dari tabel guru
        public string? namaguru { get; set; }
        public string? nip { get; set; }
    }
}