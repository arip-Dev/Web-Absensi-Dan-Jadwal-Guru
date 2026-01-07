using Latihan1.Models;
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly DapperDb _db;
        public AdminController(DapperDb db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Admin_page(string? kelas = null, int? mapelId = null, int? hari = null)
        {
            // Ambil semua jadwal (hindari overload ambigu)
            var jadwal = (await _db.GetAllJadwalAsync((int?)null)).ToList();

            // Map hari int->tanggal mendatang
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

            // Konversi mapelId -> nama mapel (untuk label filter & perbandingan jadwal.Mapel (string))
            string? selectedMapelNama = null;
            if (mapelId.HasValue)
            {
                var m = await _db.GetMapelByIdAsync(mapelId.Value);
                selectedMapelNama = m?.Nama;
            }

            // ====== FILTER OPSIONAL ======
            if (!string.IsNullOrWhiteSpace(kelas))
                jadwal = jadwal
                    .Where(j => string.Equals(j.KelasNama, kelas, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (mapelId.HasValue && !string.IsNullOrWhiteSpace(selectedMapelNama))
                jadwal = jadwal
                    .Where(j => string.Equals(j.Mapel, selectedMapelNama, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (hari.HasValue && hari.Value is >= 1 and <= 7)
                jadwal = jadwal
                    .Where(j => j.Hari == hari.Value)
                    .ToList();

            // ====== DETEKSI KONFLIK ======
            static bool Overlap(TimeSpan a1, TimeSpan a2, TimeSpan b1, TimeSpan b2)
                => a1 < b2 && a2 > b1;

            var conflictSet = new HashSet<(int GuruId, int Hari)>();

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
                    Id = j.Id,
                    Tanggal = NextDateFor(j.Hari),
                    Mulai = j.Mulai.ToString(@"hh\:mm"),
                    Selesai = j.Selesai.ToString(@"hh\:mm"),
                    Guru = j.GuruNama,
                    Mapel = j.Mapel,
                    Kelas = j.KelasNama,
                    Ruang = j.Ruangan ?? "",
                    Status = conflictSet.Contains((j.GuruId, j.Hari)) ? "Konflik" : "Terjadwal"
                })
                .OrderBy(s => s.Tanggal)
                .ThenBy(s => s.Mulai)
                .Take(50)
                .ToList();

            // ====== GURU & BEBAN MENGAJAR ======
            var loads = await _db.GetTeacherLoadsAsync();

            // 1 jp = 60 menit
            const double MENIT_PER_JP = 60.0;

            var teachers = loads
                .Select(x =>
                {
                    // konversi menit -> jp, dibulatkan ke terdekat
                    var loadJp = (int)Math.Round(
                        x.LoadMinutes / MENIT_PER_JP,
                        MidpointRounding.AwayFromZero
                    );

                    return new TeacherVM
                    {
                        Nama = x.Nama,
                        Mapel = x.Mapel,
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
                SesiBerjalanHariIni = 0, // tidak dipakai di UI saat ini
                KonflikTerbuka = 0, // tidak dipakai di UI saat ini
                GuruAktif = counts.GuruAktif
            };

            // ====== DATA UNTUK FILTER DI VIEW ======
            var kelasList = (await _db.GetKelasListAsync())
                .Select(k => k.Nama)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var mapelList = (await _db.GetAllMapelAsync())
                .OrderBy(m => m.Nama)
                .ToList();

            ViewBag.KelasList = kelasList;
            ViewBag.MapelList = mapelList;
            ViewBag.HariSelected = hari;
            ViewBag.KelasSelected = kelas;
            ViewBag.MapelSelected = mapelId;
            ViewBag.KelasCount = kelasList.Count;
            ViewBag.MapelCount = mapelList.Count;

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
