// ==================== FILE: Models/PaymentStatus.cs ====================
// Tạo file này trong thư mục Models/

namespace QuanLyRapPhim.Models
{
    /// <summary>
    /// Static class for managing payment status constants and display helpers
    /// Database stores English values, display text can be localized
    /// </summary>
    public static class PaymentStatus
    {
        // ==================== CONSTANTS (Stored in Database) ====================
        /// <summary>Payment completed successfully</summary>
        public const string Completed = "Completed";

        /// <summary>Payment is pending</summary>
        public const string Pending = "Pending";

        /// <summary>Payment failed</summary>
        public const string Failed = "Failed";

        /// <summary>Payment cancelled</summary>
        public const string Cancelled = "Cancelled";

        // ==================== VALIDATION ====================
        /// <summary>
        /// Check if status is valid
        /// </summary>
        public static bool IsValid(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            return status == Completed ||
                   status == Pending ||
                   status == Failed ||
                   status == Cancelled;
        }

        // ==================== DISPLAY TEXT ====================
        /// <summary>
        /// Get display text based on language
        /// Default: English
        /// </summary>
        /// <param name="status">Status from database</param>
        /// <param name="language">Language code (en/vi)</param>
        /// <returns>Display text</returns>
        public static string GetDisplayText(string status, string language = "en")
        {
            if (string.IsNullOrWhiteSpace(status))
                return language?.ToLower() == "vi" ? "Không xác định" : "Unknown";

            // Vietnamese
            if (language?.ToLower() == "vi")
            {
                return status switch
                {
                    Completed => "Đã thanh toán",
                    Pending => "Đang chờ",
                    Failed => "Thất bại",
                    Cancelled => "Đã hủy",
                    _ => status
                };
            }

            // English (default)
            return status switch
            {
                Completed => "Completed",
                Pending => "Pending",
                Failed => "Failed",
                Cancelled => "Cancelled",
                _ => status
            };
        }

        // ==================== CSS STYLING ====================
        /// <summary>
        /// Get Bootstrap badge CSS class for status
        /// </summary>
        public static string GetBadgeClass(string status)
        {
            return status switch
            {
                Completed => "bg-success",    // Green
                Pending => "bg-warning",      // Yellow
                Failed => "bg-danger",        // Red
                Cancelled => "bg-secondary",  // Gray
                _ => "bg-secondary"           // Default: Gray
            };
        }

        /// <summary>
        /// Get Font Awesome icon for status
        /// </summary>
        public static string GetIcon(string status)
        {
            return status switch
            {
                Completed => "fa-check-circle",
                Pending => "fa-clock",
                Failed => "fa-times-circle",
                Cancelled => "fa-ban",
                _ => "fa-question-circle"
            };
        }

        // ==================== HELPER METHODS ====================
        /// <summary>
        /// Get all possible statuses
        /// </summary>
        public static List<string> GetAllStatuses()
        {
            return new List<string>
            {
                Completed,
                Pending,
                Failed,
                Cancelled
            };
        }

        /// <summary>
        /// Get status dictionary with display text
        /// </summary>
        public static Dictionary<string, string> GetStatusDictionary(string language = "en")
        {
            var statuses = GetAllStatuses();
            return statuses.ToDictionary(
                status => status,
                status => GetDisplayText(status, language)
            );
        }

        /// <summary>
        /// Check if status is completed
        /// </summary>
        public static bool IsCompleted(string status)
        {
            return status == Completed;
        }

        /// <summary>
        /// Check if status is pending
        /// </summary>
        public static bool IsPending(string status)
        {
            return status == Pending;
        }

        /// <summary>
        /// Check if status is failed or cancelled
        /// </summary>
        public static bool IsFailedOrCancelled(string status)
        {
            return status == Failed || status == Cancelled;
        }
    }
}