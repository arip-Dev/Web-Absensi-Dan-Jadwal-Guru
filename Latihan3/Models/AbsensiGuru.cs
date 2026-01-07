using System;
using System.ComponentModel.DataAnnotations;

namespace Latihan3.Models
{
    public class AbsensiGuru
    {
        [Key]
        public int AbsensiId { get; set; }

        [Display(Name = "Guru")]
        public int Id { get; set; } // Ini GuruId (FK)

        [Display(Name = "Tanggal")]
        [DataType(DataType.DateTime)]
        public DateTime Tanggal { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        public string? Keterangan { get; set; }

        // --- TAMBAHAN (Untuk Menampilkan Data JOIN) ---
        // Properti ini tidak disimpan ke tabel Absensi, tapi dibaca dari tabel Guru
        public string? NamaGuru { get; set; }
        public string? NIP { get; set; }
    }
}