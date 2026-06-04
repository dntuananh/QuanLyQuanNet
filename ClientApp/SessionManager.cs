using System;
using System.IO;
using System.Text.Json;
using SharedModels.Models;

namespace ClientApp
{
    // Quản lý phiên làm việc của người dùng để dễ khôi phục sau khi mất kết nối
    public class SessionManager
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuanLyQuanNet",
            "session.json"
        );

        public class SessionData
        {
            public User User { get; set; }
            public int TimeRemainingSeconds { get; set; }
            public decimal Balance { get; set; }
            public DateTime SessionStartTime { get; set; }
            public string ComputerName { get; set; }
        }
        // Lưu phiên làm việc
        public static void SaveSession(User user, int timeRemainingSeconds, string computerName)
        {
            try
            {
                var directory = Path.GetDirectoryName(SessionFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var sessionData = new SessionData
                {
                    User = user,
                    TimeRemainingSeconds = timeRemainingSeconds,
                    Balance = user?.Balance ?? 0,
                    SessionStartTime = DateTime.Now,
                    ComputerName = computerName
                };

                var json = JsonSerializer.Serialize(sessionData);
                File.WriteAllText(SessionFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save session: {ex.Message}");
            }
        }
        // Tải phiên làm việc đã lưu nếu còn hợp lệ (dưới 30 phút)
        public static SessionData LoadSession()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                    return null;

                var json = File.ReadAllText(SessionFilePath);
                var sessionData = JsonSerializer.Deserialize<SessionData>(json);

                // Validate phiên làm việc
                if (sessionData != null && 
                    (DateTime.Now - sessionData.SessionStartTime).TotalMinutes < 30)
                {
                    return sessionData;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
            }

            return null;
        }


        // Xóa phiên làm việc đã lưu
        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
            }
        }
    }
}
