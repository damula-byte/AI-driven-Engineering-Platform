using System;
using System.IO;
using Newtonsoft.Json;

namespace TIA_Copilot_CLI
{
    public class AppSettings
    {
        public string Mode { get; set; } = "USER";
        public string UserApiKey { get; set; } = "";
        [JsonIgnore] 
        public bool IsKeyMissing 
        {
            get 
            {
                return string.IsNullOrWhiteSpace(UserApiKey);
            }
        }
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            string json = File.ReadAllText(SettingsPath);
            return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
    }
    public static class KeyManager
    {
        // --- TUYỆT KỸ ĐỌC PASSWORD ẨN (MASKED PASSWORD) ---
        private static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // Xóa ký tự cuối trong chuỗi pass
                        password = password.Substring(0, password.Length - 1);
                        // Lùi con trỏ trên màn hình lại 1 nhịp, in đè khoảng trắng, rồi lùi lại tiếp
                        int pos = Console.CursorLeft;
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            Console.WriteLine();
            return password;
        }

        public static void ShowKeyManagementMenu()
        {
            bool keepOpen = true;
            while (keepOpen)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("==========================================================");
                Console.WriteLine("            ⚙️ API KEY & ENGINE SETTINGS");
                Console.WriteLine("==========================================================");
                Console.ResetColor();

                // Lấy Settings từ SettingsManager mà Đăng vừa tạo
                var settings = SettingsManager.Load();

                // 1. Hiển thị trạng thái Mode
                Console.Write("\n Current Engine Mode : ");
                if (settings.Mode == "DEV")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[DEVELOPER MODE] - Using built-in internal keys.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[USER MODE] - Using customer's custom key.");
                }
                Console.ResetColor();

                // 2. Hiển thị Key hiện tại (nếu đang ở USER Mode)
                if (settings.Mode == "USER")
                {
                    Console.Write(" User API Key        : ");
                    if (string.IsNullOrWhiteSpace(settings.UserApiKey))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("(Empty - Please Add Key to use AI)");
                        Console.ResetColor();
                    }
                    else
                    {
                        string keyStr = settings.UserApiKey;
                        string masked = keyStr.Length > 15 
                            ? $"{keyStr.Substring(0, 5)}...{keyStr.Substring(keyStr.Length - 5)}" 
                            : "********";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(masked);
                        Console.ResetColor();
                    }
                }

                // 3. Menu thao tác
                Console.WriteLine("\n----------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" [M]: Toggle Mode | [A]: Add/Update Key | [D]: Clear Key | [ESC]: Back");
                Console.ResetColor();

                // 4. Bắt sự kiện phím
                var keyInfo = Console.ReadKey(intercept: true);
                char key = char.ToUpper(keyInfo.KeyChar);

                if (keyInfo.Key == ConsoleKey.Escape) 
                {
                    keepOpen = false;
                }
                else if (key == 'M')
                {
                    if (settings.Mode == "USER")
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("\n 🔒 Enter Developer Password: ");
                        Console.ResetColor();
                        
                        string pwd = ReadPassword(); 
                        
                        // Pass bí mật của bạn ở đây:
                        if (pwd == "Admin@1234") 
                        {
                            settings.Mode = "DEV";
                            SettingsManager.Save(settings);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine(" [√] Access Granted! Switched to DEVELOPER Mode.");
                            Console.ResetColor();
                            System.Threading.Thread.Sleep(1200);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(" [×] Incorrect Password! Access Denied.");
                            Console.ResetColor();
                            System.Threading.Thread.Sleep(1500);
                        }
                    }
                    else
                    {
                        // Đang ở DEV, quay về USER tự do
                        settings.Mode = "USER";
                        SettingsManager.Save(settings);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n [√] Switched back to USER Mode.");
                        Console.ResetColor();
                        System.Threading.Thread.Sleep(800);
                    }
                }
                else if (key == 'A' && settings.Mode == "USER")
                {
                    Console.Write("\n 👉 Enter your Gemini API Key (Right-click to paste): ");
                    string newKey = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(newKey))
                    {
                        settings.UserApiKey = newKey;
                        SettingsManager.Save(settings);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(" [√] API Key updated successfully!");
                        Console.ResetColor();
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                else if (key == 'D' && settings.Mode == "USER")
                {
                    Console.Write("\n ⚠️ Are you sure you want to clear your API Key? (y/n): ");
                    if (Console.ReadLine()?.Trim().ToLower() == "y")
                    {
                        settings.UserApiKey = "";
                        SettingsManager.Save(settings);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(" [√] API Key cleared!");
                        Console.ResetColor();
                        System.Threading.Thread.Sleep(1000);
                    }
                }
                else if ((key == 'A' || key == 'D') && settings.Mode == "DEV")
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n [i] You are in DEV Mode. Internal keys are managed in Python source code.");
                    Console.ResetColor();
                    System.Threading.Thread.Sleep(1500);
                }
            }
        }
    }
}

