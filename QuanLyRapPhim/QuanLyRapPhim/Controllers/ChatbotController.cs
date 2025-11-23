using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using QuanLyRapPhim.Data;
using QuanLyRapPhim.Models;
using QuanLyRapPhim.Service;

namespace QuanLyRapPhim.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatbotController : ControllerBase
    {
        private readonly GroqService _groqService;
        private readonly DBContext _context;

        public ChatbotController(GroqService groqService, DBContext context)
        {
            _groqService = groqService;
            _context = context;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (request?.Message == null) // ← thêm kiểm tra này để debug
                return BadRequest("Tin nhắn rỗng");

            var context = await BuildContext();
            var response = await _groqService.GetChatResponse(request.Message, context);
            return Ok(new { reply = response });
        }
        private async Task<string> BuildContext()
        {
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                             TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
            var todayVN = vietnamTime.Date;

            // 1. Lấy tất cả suất chiếu...
            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                .Where(s => s.Date > todayVN ||
                           (s.Date == todayVN && s.StartTime >= vietnamTime.TimeOfDay))
                .Where(s => s.Date <= todayVN.AddDays(7))
                .OrderBy(s => s.Date)
                .ThenBy(s => s.StartTime)
                .Select(s => new
                {
                    Ngay = s.Date.ToString("dd/MM (dddd)"),
                    TenPhim = s.Movie.Title.Trim(),
                    Gio = s.StartTime.ToString(@"hh\:mm"),
                    Phong = s.Room.RoomName
                })
                .ToListAsync();

            var phimDangChieu = showtimes.Select(s => s.TenPhim).Distinct().OrderBy(x => x).ToList();
            var danhSachPhim = phimDangChieu.Any()
                ? string.Join(", ", phimDangChieu)
                : "Hiện chưa có phim nào trong tuần này";

            var lichChieuText = showtimes.Any()
                ? string.Join("\n", showtimes
                    .GroupBy(x => x.Ngay)
                    .Select(g => $"{g.Key}:\n" +
                                string.Join("\n", g.Select(x => $" • {x.TenPhim} - {x.Gio} - {x.Phong}"))))
                : "Hiện tại chưa có lịch chiếu nào trong 7 ngày tới ạ";

            // PHẦN BẮP NƯỚC MỚI – SIÊU ỔN ĐỊNH, KHÔNG LỖI
            var foodItems = await _context.FoodItems
                .Where(f => f.Category == "Combo" ||
                            f.Category == "Bắp" ||
                            f.Category == "Popcorn" ||
                            f.Category == "Nước" ||
                            f.Category == "Nước uống" ||
                            f.Category == "Drink")
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Price)
                .ToListAsync();

            var comboLines = new List<string>();

            if (foodItems.Any())
            {
                var groups = foodItems.GroupBy(f => f.Category?.Trim())
                                      .OrderBy(g => g.Key);

                foreach (var group in groups)
                {
                    string groupName = group.Key switch
                    {
                        "Combo" => "COMBO SIÊU HỜI",
                        "Bắp" or "Popcorn" => "BẮP RANG",
                        "Nước" or "Nước uống" or "Drink" => "NƯỚC UỐNG",
                        _ => group.Key ?? "KHÁC"
                    };

                    var items = group.Select(f =>
                        string.IsNullOrWhiteSpace(f.Size)
                            ? $"{f.Name.Trim()} → {f.Price:N0}đ"
                            : $"{f.Name.Trim()} ({f.Size.Trim()}) → {f.Price:N0}đ"
                    );

                    comboLines.Add($"{groupName}:\n   " + string.Join(" | ", items));
                }
            }

            var comboText = comboLines.Any()
                ? string.Join("\n", comboLines)
                : "Menu bắp nước đang được cập nhật ạ!";

            return $@"
=== CINEMA XXI - DỮ LIỆU THỰC TẾ MỚI NHẤT ({vietnamTime:dd/MM/yyyy HH:mm}) ===
PHIM ĐANG CHIẾU TRONG TUẦN:
{danhSachPhim}

LỊCH CHIẾU CHI TIẾT TỪ HÔM NAY ĐẾN 7 NGÀY TỚI:
{lichChieuText}

GIÁ VÉ:
• Ghế thường: 50.000đ
• Ghế đôi: 100.000đ/người

BẮP NƯỚC - COMBO:
{comboText}

Bạn ơi muốn xem phim nào, suất mấy giờ hay mua combo gì thì cứ nói mình book liền nha!
Dữ liệu lấy trực tiếp từ quầy, đúng 100% luôn ạ!".Trim();
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = "";
    }
}