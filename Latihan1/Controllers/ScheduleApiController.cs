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
        public async Task<IActionResult> Getmapel()
        {
            var mapel = await _db.GetDistinctMapelAsync();
            return Ok(mapel);
        }

        // { id, tingkat, namaSingkat }
        [HttpGet("kelas")]
        public async Task<IActionResult> Getkelas()
        {
            var kelas = await _db.GetKelasListAsync();
            var dto = kelas.Select(k => new { id = k.Id, tingkat = k.Tingkat, namaSingkat = k.Nama });
            return Ok(dto);
        }

        // GET: /api/schedule/guru?mapel=Matematika
        [HttpGet("guru")]
        public async Task<IActionResult> GetguruBymapel([FromQuery] string mapel)
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
                    maxweeklyload = g.MaxWeeklyLoad,
                    currentLoadMinutes = minutes,
                    currentLoadHours = hours
                };
            });

            return Ok(dto);
        }

        // Alias lama
        [HttpGet("guru-by-mapel")]
        public Task<IActionResult> GetguruBymapelAlias([FromQuery] string mapel)
            => GetguruBymapel(mapel);

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
                    guruid = r.GuruId,
                    guru = r.GuruNama,
                    mapel = r.Mapel,
                    hari = r.Hari,                 // 1..7
                    mulai = ToHm(r.Mulai),
                    selesai = ToHm(r.Selesai),
                    kelasid = r.KelasId,
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
            int guruid, string mapel, int hari,
            string mulai, string selesai,
            int kelasid, string? ruangan
        );

        private static bool TryParseTime(string s, out TimeSpan t)
            => TimeSpan.TryParse(s, out t);

        [HttpPost("check")]
        public async Task<IActionResult> Check([FromBody] CreateReq req)
        {
            if (req is null) return BadRequest(new { conflict = true, message = "Body kosong." });

            if (!TryParseTime(req.mulai, out var mulai) || !TryParseTime(req.selesai, out var selesai))
                return Ok(new { conflict = true, message = "Format waktu salah. Gunakan 'HH:mm'." });

            // Cek 1: Bentrok guru?
            var conflictguru = await _db.HasScheduleConflictAsync(req.guruid, req.hari, mulai, selesai, null);
            if (conflictguru)
            {
                return Ok(new { conflict = true, message = "guru ini sudah mengajar di jam tersebut." });
            }

            // Cek 2: Bentrok kelas? (TAMBAHAN)
            var conflictkelas = await _db.HasClassScheduleConflictAsync(req.kelasid, req.hari, mulai, selesai, null);
            if (conflictkelas)
            {
                return Ok(new { conflict = true, message = "kelas ini sudah ada jadwal pelajaran lain di jam tersebut." });
            }

            return Ok(new { conflict = false });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateReq req)
        {
            if (req is null) return BadRequest("Body kosong.");

            if (!TryParseTime(req.mulai, out var mulai) || !TryParseTime(req.selesai, out var selesai))
                return BadRequest("Format waktu salah. Gunakan 'HH:mm' (mis. 07:00).");

            if (string.CompareOrdinal(req.selesai, req.mulai) <= 0)
                return BadRequest("Waktu selesai harus lebih besar dari waktu mulai.");

            // Cek 1: Bentrok guru?
            var conflictguru = await _db.HasScheduleConflictAsync(req.guruid, req.hari, mulai, selesai, null);
            if (conflictguru)
                return Conflict(new { message = "Bentrok: guru tersebut sedang mengajar di kelas lain pada jam ini." });

            // Cek 2: Bentrok kelas? (TAMBAHAN)
            var conflictkelas = await _db.HasClassScheduleConflictAsync(req.kelasid, req.hari, mulai, selesai, null);
            if (conflictkelas)
                return Conflict(new { message = "Bentrok: kelas ini sudah memiliki jadwal pelajaran lain pada jam ini." });

            var newid = await _db.CreateJadwalAsync(
                req.guruid, req.mapel, req.hari, mulai, selesai, req.kelasid, req.ruangan
            );

            return Ok(new { id = newid });
        }

        [HttpPost("create")]
        public Task<IActionResult> CreateAlias([FromBody] CreateReq req) => Create(req);

        //UPDATE jadwal
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateReq req)
        {
            if (req is null) return BadRequest("Body kosong.");

            if (!TryParseTime(req.mulai, out var mulai) || !TryParseTime(req.selesai, out var selesai))
                return BadRequest("Format waktu salah. Gunakan 'HH:mm' (mis. 07:00).");

            if (string.CompareOrdinal(req.selesai, req.mulai) <= 0)
                return BadRequest("Waktu selesai harus lebih besar dari waktu mulai.");

            // Cek 1: Bentrok guru? (Exclude id yang sedang diedit)
            var conflictguru = await _db.HasScheduleConflictAsync(req.guruid, req.hari, mulai, selesai, id);
            if (conflictguru)
                return Conflict(new { message = "Bentrok: guru tersebut sedang mengajar di kelas lain pada jam ini." });

            // Cek 2: Bentrok kelas? (Exclude id yang sedang diedit) (TAMBAHAN)
            var conflictkelas = await _db.HasClassScheduleConflictAsync(req.kelasid, req.hari, mulai, selesai, id);
            if (conflictkelas)
                return Conflict(new { message = "Bentrok: kelas ini sudah memiliki jadwal pelajaran lain pada jam ini." });

            var affected = await _db.UpdateJadwalAsync(
                id, req.guruid, req.mapel, req.hari, mulai, selesai, req.kelasid, req.ruangan
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
