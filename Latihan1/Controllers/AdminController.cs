using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq; // Pastikan ini ada untuk operasi Dictionary/Group

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DapperDb _db;
        public AdminController(DapperDb db) => _db = db;

        [HttpGet]
        // PERBAIKAN 1: Tambahkan parameter string? tingkat
        public async Task<IActionResult> Admin_page(string? tingkat = null, string? kelas = null, int? mapelid = null, int? hari = null)
        {
            var jadwal = (await _db.GetAllJadwalAsync((int?)null)).ToList();

            static int DayNum(DateTime d) => d.DayOfWeek switch
            {
                DayOfWeek.Monday => 1,
                DayOfWeek.Tuesday => 2,
                DayOfWeek.Wednesday => 3,
                DayOfWeek.Thursday => 4,
                DayOfWeek.Friday => 5,
                DayOfWeek.Saturday => 6,
                DayOfWeek.Sunday => 7,
                _ => 1
            };

            DateTime today = DateTime.Today;
            int todayNum = DayNum(today);

            DateTime NextDateFor(int targetDay)
            {
                int delta = (targetDay - todayNum + 7) % 7;
                if (delta == 0) delta = 7;
                return today.AddDays(delta);
            }

            string? selectedmapelnama = null;
            if (mapelid.HasValue)
            {
                var m = await _db.GetMapelByIdAsync(mapelid.Value);
                selectedmapelnama = m?.nama;
            }

            // ====== FILTER TINGKAT & KELAS ======
            if (!string.IsNullOrWhiteSpace(tingkat))
            {
                if (!string.IsNullOrWhiteSpace(kelas))
                {
                    // Jika Admin memilih Tingkat "X" dan Kelas "IPA1" -> cari string persis "X IPA1"
                    string targetKelas = $"{tingkat} {kelas}";
                    jadwal = jadwal.Where(j => string.Equals(j.KelasNama, targetKelas, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                else
                {
                    // Jika Admin hanya memilih Tingkat "X" -> Tampilkan SEMUA kelas X
                    // Tambahkan spasi di belakang tingkat agar "X " tidak keliru membaca "XI"
                    string targetPrefix = $"{tingkat} ";
                    jadwal = jadwal.Where(j => j.KelasNama != null && j.KelasNama.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
                }
            }

            // ====== FILTER MAPEL & HARI ======
            if (mapelid.HasValue && !string.IsNullOrWhiteSpace(selectedmapelnama))
                jadwal = jadwal.Where(j => string.Equals(j.Mapel, selectedmapelnama, StringComparison.OrdinalIgnoreCase)).ToList();

            if (hari.HasValue && hari.Value is >= 1 and <= 7)
                jadwal = jadwal.Where(j => j.Hari == hari.Value).ToList();


            // ====== DETEKSI KONFLIK ======
            static bool Overlap(TimeSpan a1, TimeSpan a2, TimeSpan b1, TimeSpan b2)
                => a1 < b2 && a2 > b1;

            var conflictSet = new HashSet<(int guruid, int hari)>();

            foreach (var g in jadwal.GroupBy(x => new { x.GuruId, x.Hari }))
            {
                var items = g.OrderBy(x => x.Mulai).ToList();
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if (Overlap(items[i].Mulai, items[i].Selesai, items[j].Mulai, items[j].Selesai))
                        {
                            conflictSet.Add((g.Key.GuruId, g.Key.Hari));
                        }
                    }
                }
            }

            // ====== SESSION VM ======
            var sessions = jadwal
                .Select(j => new SessionVM
                {
                    id = j.Id,
                    tanggal = NextDateFor(j.Hari),
                    mulai = j.Mulai.ToString(@"hh\:mm"),
                    selesai = j.Selesai.ToString(@"hh\:mm"),
                    guru = j.GuruNama,
                    mapel = j.Mapel,
                    kelas = j.KelasNama,
                    Ruang = j.Ruangan ?? "",
                    status = conflictSet.Contains((j.GuruId, j.Hari)) ? "Konflik" : "Terjadwal"
                })
                .OrderBy(s => s.tanggal)
                .ThenBy(s => s.mulai)
                .Take(50)
                .ToList();

            // ====== BEBAN MENGAJAR ======
            var loads = await _db.GetTeacherLoadsAsync();
            const double MENIT_PER_JP = 60.0;
            var teachers = loads
                .Select(x =>
                {
                    var loadJp = (int)Math.Round(x.LoadMinutes / MENIT_PER_JP, MidpointRounding.AwayFromZero);
                    return new TeacherVM
                    {
                        nama = x.Nama,
                        mapel = x.Mapel,
                        Load = loadJp,
                        MaxLoad = x.MaxWeeklyLoad
                    };
                })
                .OrderByDescending(t => t.Load)
                .Take(8)
                .ToList();

            // ====== STATISTIK DASAR DASHBOARD ======
            var counts = await _db.GetDashboardCountsAsync(todayNum);
            var stats = new DashboardStatsVM
            {
                TotalSesiMingguIni = counts.TotalSesi,
                SesiBerjalanhariIni = 0,
                KonflikTerbuka = 0,
                guruAktif = counts.GuruAktif
            };

            // ====== DATA UNTUK FILTER BERTINGKAT DI VIEW ======
            var rawKelas = await _db.GetKelasListAsync();

            // 1. Ambil daftar tingkat yang unik (Contoh: "X", "XI", "XII")
            var tingkatList = rawKelas.Select(k => k.Tingkat).Distinct().OrderBy(t => t).ToList();

            // 2. Buat Dictionary: Key = Tingkat, Value = Daftar Nama Kelas (Contoh: "X" => ["IPA1", "IPS1"])
            var kelasDict = rawKelas
                .GroupBy(k => k.Tingkat)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(k => k.Nama).Distinct().OrderBy(n => n).ToList()
                );

            var mapelList = (await _db.GetAllMapelAsync()).OrderBy(m => m.nama).ToList();

            // Kirim ke ViewBag
            ViewBag.tingkatList = tingkatList;
            ViewBag.kelasDict = kelasDict;
            ViewBag.mapelList = mapelList;

            ViewBag.tingkatSelected = tingkat;
            ViewBag.kelasSelected = kelas;
            ViewBag.mapelSelected = mapelid;
            ViewBag.hariSelected = hari;

            var vm = new AdminDashboardViewModel
            {
                UpcomingSessions = sessions,
                Teachers = teachers,
                Stats = stats
            };

            return View("~/Views/Adminpage/Admin_page.cshtml", vm);
        }
    }
}