namespace Latihan1.Models
{
    public sealed class SubjectItem
    {
        public string mapel { get; set; } = "";
        public int Count { get; set; }
    }

    public sealed class SubjectsVm
    {
        public IEnumerable<SubjectItem>? Subjects { get; set; }  // untuk grid
        public string? SubjectSelected { get; set; }              // untuk detail
        public IEnumerable<guruModel>? Teachers { get; set; }     // untuk detail
    }
}
