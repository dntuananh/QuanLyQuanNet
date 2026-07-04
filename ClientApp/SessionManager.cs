using System;
using System.IO;
using System.Text.Json;
using SharedModels.Models;

namespace ClientApp
{
    public class SessionManager
    {
        private static readonly string SessionFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QuanLyQuanNet",
            "session_identity.json"
        );

        public class SessionIdentity
        {
            public User User { get; set; } = null!;
            public DateTime SavedTime { get; set; }
            public string ComputerName { get; set; } = string.Empty;
        }

        public static void SaveSessionIdentity(User user, string computerName)
        {
            try
            {
                var directory = Path.GetDirectoryName(SessionFilePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                var data = new SessionIdentity
                {
                    User = user,
                    SavedTime = DateTime.Now,
                    ComputerName = computerName
                };

                File.WriteAllText(SessionFilePath, JsonSerializer.Serialize(data));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save session identity: {ex.Message}");
            }
        }

        public static SessionIdentity? LoadSessionIdentity()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                    return null;

                var data = JsonSerializer.Deserialize<SessionIdentity>(File.ReadAllText(SessionFilePath));

                if (data != null && (DateTime.Now - data.SavedTime).TotalMinutes < 30)
                    return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load session: {ex.Message}");
            }

            return null;
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                    File.Delete(SessionFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear session: {ex.Message}");
            }
        }
    }
}
