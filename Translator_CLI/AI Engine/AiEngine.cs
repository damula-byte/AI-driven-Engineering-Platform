using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TIA_Copilot_CLI
{
    public static class AiEngine
    {
        public static string PYTHON_EXE_PATH = "";
        public static string PYTHON_SCRIPT_PATH = ""; 

        public static void InitializePaths()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo currentDir = new DirectoryInfo(baseDir);
            string backendFolder = null;

            int maxDepth = 5;
            while (currentDir != null && maxDepth > 0)
            {
                string potentialPath = Path.Combine(currentDir.FullName, "AI_Backend");
                if (Directory.Exists(potentialPath))
                {
                    backendFolder = potentialPath;
                    break;
                }
                currentDir = currentDir.Parent;
                maxDepth--;
            }

            if (backendFolder != null)
            {
                // ƯU TIÊN 1: MÔI TRƯỜNG DEPLOY (Tìm file exe đã đóng gói)
                // PyInstaller thường xuất file ra thư mục dist\ai_engine\ai_engine.exe
                string distExePath = Path.Combine(backendFolder, "dist", "ai_engine", "ai_engine.exe");
                // Hoặc thư mục release cuối cùng nếu bạn đã copy ra ngoài
                string releaseExePath = Path.Combine(backendFolder, "ai_engine.exe");

                if (File.Exists(releaseExePath))
                {
                    PYTHON_EXE_PATH = releaseExePath;
                    PYTHON_SCRIPT_PATH = ""; // Không cần truyền script nữa
                }
                else if (File.Exists(distExePath))
                {
                    PYTHON_EXE_PATH = distExePath;
                    PYTHON_SCRIPT_PATH = ""; // Không cần truyền script nữa
                }
                // ƯU TIÊN 2: MÔI TRƯỜNG DEV (Fallback về python.exe và main.py)
                else
                {
                    PYTHON_EXE_PATH = Path.Combine(backendFolder, "env", "python.exe");
                    if (!File.Exists(PYTHON_EXE_PATH))
                    {
                        PYTHON_EXE_PATH = Path.Combine(backendFolder, "env", "Scripts", "python.exe");
                    }
                    // Nếu dùng conda, bạn có thể phải trỏ thẳng tới python.exe của conda ở đây
                    PYTHON_SCRIPT_PATH = Path.Combine(backendFolder, "main.py");
                }
            }
        }

        public static async Task<string> CallPythonBackendAsync(string query, string sessionId, string commandType, string contextCode = "", string specText = "", string targetType = "AUTO", string userTags = "")
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = PYTHON_EXE_PATH;
                
                // [QUAN TRỌNG]: Đổi logic gán Argument
                // Nếu chạy bản Dev (có path tới main.py) -> truyền argument
                // Nếu chạy bản Deploy (file exe) -> để trống argument
                if (!string.IsNullOrEmpty(PYTHON_SCRIPT_PATH))
                {
                    start.Arguments = $"\"{PYTHON_SCRIPT_PATH}\"";
                    start.WorkingDirectory = Path.GetDirectoryName(PYTHON_SCRIPT_PATH);
                }
                else
                {
                    start.Arguments = "";
                    start.WorkingDirectory = Path.GetDirectoryName(PYTHON_EXE_PATH);
                }

                start.UseShellExecute = false;
                start.RedirectStandardInput = true;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                start.StandardOutputEncoding = Encoding.UTF8;

                using (Process process = Process.Start(start))
                {
                    var payload = new
                    {
                        query = query,
                        session_id = sessionId,
                        command = commandType,
                        context_code = contextCode,
                        spec_text = specText,
                        target_block_type = targetType,
                        user_tags = userTags
                    };

                    string jsonInput = JsonConvert.SerializeObject(payload);

                    // 1. [KHẮC PHỤC BOM & FLUSH]
                    using (StreamWriter writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        await writer.WriteAsync(jsonInput);
                        await writer.FlushAsync();
                    }

                    // 2. TUYỆT KỸ PHÁ DEADLOCK
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    
                    await Task.WhenAll(outputTask, errorTask);

                    // 3. [KHẮC PHỤC DOUBLE READ]
                    string result = outputTask.Result;
                    string error = errorTask.Result;

                    await Task.Run(() => process.WaitForExit());

                    if (!string.IsNullOrEmpty(error) && string.IsNullOrWhiteSpace(result))
                    {
                        return JsonConvert.SerializeObject(new { status = "error", message = error });
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { status = "error", message = ex.Message });
            }
        }
    }
}