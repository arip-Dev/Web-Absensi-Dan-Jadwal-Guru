// Controllers/ScheduleApiController.cs
using Latihan1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Latihan1.Controllers
{
    [Authorize(AuthenticationSchemes = "CookieSekolah", Roles = "Admin")]
    [ApiController]
    [Route("api/schedule")]
    public class ScheduleApiController : ControllerBase
    {
        private readonly DapperDb _db;
        public ScheduleApiController(DapperDb db) => _db = db;

        // =======================
        // Helpers
        // =======================
        private static string DayNumToName(int n) => n switch
        {
            1 => "Senin",
            2 => "Selasa",
            3 => "Rabu",
            4 => "Kamis",
            5 => "Jumat",
            6 => "Sabtu",
            7 => "Minggu",
            _ => "Senin"
        };

        private static string ToHm(TimeSpan t) => $"{t.Hours:D2}:{t.Minutes:D2}";

        // =======================
        // LOOKUPS
        // =======================

        [HttpGet("mapel")]
        public async Task<IActionResult> GetMapel()
        {
            var mapel = await _db.GetDistinctMapelAsync();
            return Ok(mapel);
        }

        // { id, tingkat, namaSingkat }
        [HttpGet("kelas")]
        public async Task<IActionResult> GetKelas()
        {
            var kelas = await _db.GetKelasListAsync();
            var dto = kelas.Select(k => new { id = k.Id, tingkat = k.Tingkat, namaSingkat = k.Nama });
            return Ok(dto);
        }

        // GET: /api/schedule/guru?mapel=Matematika
        [HttpGet("guru")]
        public async Task<IActionResult> GetGuruByMapel([FromQuery] string mapel)
        {
            if (string.IsNullOrWhiteSpace(mapel))
                return BadRequest("Parameter 'mapel' wajib.");

            var rows = await _db.GetGuruByMapelWithLoadAsync(mapel);

            var dto = rows.Select(g =>
            {
                var minutes = g.CurrentLoad;
                var hours = Math.Round(minutes / 60.0, 1);
                return new
                {
                    id = g.Id,
                    nama = g.Guru,
                    mapel = g.Mapel,
                    maxWeeklyLoad = g.MaxWeeklyLoad,
                    currentLoadMinutes = minutes,
                    currentLoadHours = hours
                };
            });

            return Ok(dto);
        }

        // Alias lama
        [HttpGet("guru-by-mapel")]
        public Task<IActionResult> GetGuruByMapelAlias([FromQuery] string mapel)
            => GetGuruByMapel(mapel);

        // =======================
        // LIST
        // =======================

        // GET: /api/schedule?hari=1..7  (DB & model sudah angka
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? hari = null)
        {
            try
            {
                var rows = await _db.GetAllJadwalAsync(hari);

                string ToHm(TimeSpan t) => $"{t.Hours:D2}:{t.Minutes:D2}";

                var dto = rows.Select(r => new
                {
                    id = r.Id,
                    guruId = r.GuruId,
                    guru = r.GuruNama,
                    mapel = r.Mapel,
                    hari = r.Hari,                 // 1..7
                    mulai = ToHm(r.Mulai),
                    selesai = ToHm(r.Selesai),
                    kelasId = r.KelasId,
                    kelas = r.KelasNama,
                    ruangan = r.Ruangan
                });

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return Problem(title: "List jadwal gagal", detail: ex.Message, statusCode: 500);
            }
        }


        // Alias buat JS
        [HttpGet("list")]
        public Task<IActionResult> ListAlias([FromQuery] int? hari = null)
           => List(hari);

        // =======================
        // CHECK & CREATE
        // =======================

        public record CreateReq(
            int GuruId, string Mapel, int Hari,
            string Mulai, string Selesai,
            int KelasId, string? Ruangan
        );

        private static bool TryParseTime(string s, out TimeSpan t)
            => TimeSpan.TryParse(s, out t);

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] CreateReq req)
        {
            if (req is null) return BadRequest(new { conflict = true, message = "Body kosong." });

            if (!TryParseTime(req.Mulai, out var mulai) || !TryParseTime(req.Selesai, out var selesai))
                return Ok(new { conflict = true, message = "Format waktu salah. Gunakan 'HH:mm'." });

            // Cek 1: Bentrok Guru?
            var conflictGuru = await _db.HasScheduleConflictAsync(req.GuruId, req.Hari, mulai, selesai, null);
            if (conflictGuru)
            {
                return Ok(new { conflict = true, message = "Guru ini sudah mengajar di jam tersebut." });
            }

            // Cek 2: Bentrok Kelas? (TAMBAHAN)
            var conflictKelas = await _db.HasClassScheduleConflictAsync(req.KelasId, req.Hari, mulai, selesai, null);
            if (conflictKelas)
            {
                return Ok(new { conflict = true, message = "Kelas ini sudah ada jadwal pelajaran lain di jam tersebut." });
            }

            return Ok(new { conflict = false });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReq req)
        {
            if (req is null) return BadRequest("Body kosong.");

            if (!TryParseTime(req.Mulai, out var mulai) || !TryParseTime(req.Selesai, out var selesai))
                return BadRequest("Format waktu salah. Gunakan 'HH:mm' (mis. 07:00).");

            if (string.CompareOrdinal(req.Selesai, req.Mulai) <= 0)
                return BadRequest("Waktu selesai harus lebih besar dari waktu mulai.");

            // Cek 1: Bentrok Guru?
            var conflictGuru = await _db.HasScheduleConflictAsync(req.GuruId, req.Hari, mulai, selesai, null);
            if (conflictGuru)
                return Conflict(new { message = "Bentrok: Guru tersebut sedang mengajar di kelas lain pada jam ini." });

            // Cek 2: Bentrok Kelas? (TAMBAHAN)
            var conflictKelas = await _db.HasClassScheduleConflictAsync(req.KelasId, req.Hari, mulai, selesai, null);
            if (conflictKelas)
                return Conflict(new { message = "Bentrok: Kelas ini sudah memiliki jadwal pelajaran lain pada jam ini." });

            var newId = await _db.CreateJadwalAsync(
                req.GuruId, req.Mapel, req.Hari, mulai, selesai, req.KelasId, req.Ruangan
            );

            return Ok(new { Id = newId });
        }

        [HttpPost("create")]
        public Task<IActionResult> CreateAlias([FromBody] CreateReq req) => Create(req);

        //UPDATE JADWAL
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateReq req)
        {
            if (req is null) return BadRequest("Body kosong.");

            if (!TryParseTime(req.Mulai, out var mulai) || !TryParseTime(req.Selesai, out var selesai))
                return BadRequest("Format waktu salah. Gunakan 'HH:mm' (mis. 07:00).");

            if (string.CompareOrdinal(req.Selesai, req.Mulai) <= 0)
                return BadRequest("Waktu selesai harus lebih besar dari waktu mulai.");

            // Cek 1: Bentrok Guru? (Exclude ID yang sedang diedit)
            var conflictGuru = await _db.HasScheduleConflictAsync(req.GuruId, req.Hari, mulai, selesai, id);
            if (conflictGuru)
                return Conflict(new { message = "Bentrok: Guru tersebut sedang mengajar di kelas lain pada jam ini." });

            // Cek 2: Bentrok Kelas? (Exclude ID yang sedang diedit) (TAMBAHAN)
            var conflictKelas = await _db.HasClassScheduleConflictAsync(req.KelasId, req.Hari, mulai, selesai, id);
            if (conflictKelas)
                return Conflict(new { message = "Bentrok: Kelas ini sudah memiliki jadwal pelajaran lain pada jam ini." });

            var affected = await _db.UpdateJadwalAsync(
                id, req.GuruId, req.Mapel, req.Hari, mulai, selesai, req.KelasId, req.Ruangan
            );

            if (affected == 0) return NotFound();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var n = await _db.DeleteJadwalAsync(id);
            if (n == 0) return NotFound();
            return NoContent();
        }
    }
}
