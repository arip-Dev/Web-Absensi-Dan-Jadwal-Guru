// Models/guruModel.cs
using System.ComponentModel.DataAnnotations;

namespace Latihan1.Models
{
    public class guruModel
    {
        public int id { get; set; }
        public string nama { get; set; } = "";
        public string nip { get; set; } = "";
        [Required, EmailAddress]
        public string email { get; set; } = "";
        public int jammengajar { get; set; }
        public bool isactive { get; set; }
        public int maxweeklyload { get; set; }
        public int maxdailyload { get; set; }
        public int maxconsecutiveslots { get; set; }
        public int mapelid { get; set; }
        public int[] mapelids { get; set; } = Array.Empty<int>();
        public string? mapelnama { get; set; }
        public string? qrcodebase64 { get; set; }
    }

    // ViewModel untuk halaman list (paging + search)
    public class TeacherListViewModel
    {
        public IEnumerable<guruModel> Items { get; set; } = Enumerable.Empty<guruModel>();
        public string? Query { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get; set; }
    }
}
