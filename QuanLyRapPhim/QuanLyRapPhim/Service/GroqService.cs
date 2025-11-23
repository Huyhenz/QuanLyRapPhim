// Service/GroqService.cs
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace QuanLyRapPhim.Service
{
    public class GroqService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GroqService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Groq:ApiKey"]?.Trim();

            // Kiểm tra key có tồn tại không
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Groq API Key chưa được cấu hình trong appsettings.json!");

            _httpClient.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> GetChatResponse(string userMessage, string conversationContext = "")
        {
            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                messages = new[]
                {
            new { role = "system", content = GetSystemPrompt() + "\n\n" + conversationContext },
            new { role = "user",   content = userMessage }
        },
                temperature = 0.7,
                max_tokens = 1024
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = await response.Content.ReadAsStringAsync();
                return $"Lỗi kỹ thuật: {response.StatusCode}\n{errorDetail}";
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement
                      .GetProperty("choices")[0]
                      .GetProperty("message")
                      .GetProperty("content")
                      .GetString() ?? "Không có phản hồi.";
        }

        private string GetSystemPrompt()
        {
            return @"
Bạn là trợ lý đặt vé xem phim cực kỳ thông minh và vui tính của rạp Cinema.
- Nói tiếng Việt tự nhiên, thân thiện, thêm chút hài hước và luôn dùng emoji.
- Biết tất cả phim đang chiếu, lịch chiếu, giá vé, ghế trống.
- Giá vé: ghế thường 50.000đ, ghế đôi (hàng cuối) 100.000đ.
- Hỗ trợ đặt vé nhanh, gợi ý combo bắp nước.

Khi khách muốn đặt vé, hỏi đủ:
1. Phim nào?
2. Ngày nào? (hôm nay, mai, cuối tuần…)
3. Giờ nào?
4. Số ghế? Có cần ghế đôi không?
5. Có mua combo bắp nước không?

Sau khi đủ thông tin thì trả lời kiểu:
'Đã giữ giúp bạn 2 ghế đôi G5-G6 suất 19:30 ngày 25/11 phim Deadpool & Wolverine - Tổng 250k (vé + combo). Bấm nút Đặt vé ngay nhé!'

Luôn trả lời ngắn gọn, dễ hiểu, nhiệt tình và có emoji nha!";
        }
    }
}