// Models/GuruModel.cs
using System.ComponentModel.DataAnnotations;

namespace Latihan1.Models
{
    public class GuruModel
    {
        public int Id { get; set; }
        public string Nama { get; set; } = "";
        public string NIP { get; set; } = "";
        [Required, EmailAddress]
        public string Email { get; set; } = "";
        public int JamMengajar { get; set; }
        public bool IsActive { get; set; }
        public int MaxWeeklyLoad { get; set; }
        public int MaxDailyLoad { get; set; }
        public int MaxConsecutiveSlots { get; set; }
        public int MapelId { get; set; }
        public int[] MapelIds { get; set; } = Array.Empty<int>();
        public string? MapelNama { get; set; }
        public string? QRCodeBase64 { get; set; }
    }

    // ViewModel untuk halaman list (paging + search)
    public class TeacherListViewModel
    {
        public IEnumerable<GuruModel> Items { get; set; } = Enumerable.Empty<GuruModel>();
        public string? Query { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }
}
