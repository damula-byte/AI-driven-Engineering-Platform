using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml;
using System.Collections.Generic;
using Middleware_console;

namespace TIA_Copilot_CLI
{
    public static class CommandHandler
    {
        public static string DefaultSessionID = "default_session";

        private static readonly string TagCacheFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags_cache.txt");

        public static string GetBlockType(string input)
        {
            string up = input.ToUpper();
            if (up == "OB" || up == "ORGANIZATION_BLOCK") return "ORGANIZATION_BLOCK";
            if (up == "FB" || up == "FUNCTION_BLOCK") return "FUNCTION_BLOCK";
            if (up == "FC" || up == "FUNCTION") return "FUNCTION";
            if (up == "DB" || up == "DATA_BLOCK") return "DATA_BLOCK";
            if (up == "SCADA" || up == "HMI") return "HMI_SCREEN";
            if (up == "CWC") return "CWC_SCREEN";
            return "AUTO";
        }
        ///////////////////////////////////// AGENT AI //////////////////////////////////////////////////////////////////////////////////////
        public static async Task HandleAgentAsync(string query, string sessionId, TIA_V20 tiaEngine)
        {
            var settings = SettingsManager.Load();
            string keytoPass = "";

            if (settings.Mode == "USER")
            {
                if (settings.IsKeyMissing)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[API ERROR] You are in USER MODE but no API Key is configured.");
                    Console.ResetColor();
                    return;
                }
                keytoPass = settings.UserApiKey;
            }

            Console.WriteLine($"\n🤖 [AGENT MODE] Phân tích yêu cầu");

            // 1. GỌI BACKEND VỚI COMMAND = "agent_mode"
            var backendTask = AiEngine.CallPythonBackendAsync(
                query: query,
                sessionId: sessionId,
                commandType: "agent_mode", // 🌟 Khóa định tuyến sang LangChain Tools bên Python
                contextCode: "",
                specText: "",
                targetType: "AUTO",
                userTags: "",
                systemMode: settings.Mode,
                customApiKey: keytoPass
            );

            string jsonResponse = await RunWithSpinner(backendTask, "Agent is thinking & gathering tools...");

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[CRITICAL ERROR]: Python backend did not return any response.");
                Console.ResetColor();
                return;
            }

            // 2. BÓC TÁCH VÀ THI HÀNH ÁN
            try
            {
                int startIndex = jsonResponse.IndexOf('{');
                int endIndex = jsonResponse.LastIndexOf('}');
                if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                {
                    jsonResponse = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                }

                JObject jsonResult = JObject.Parse(jsonResponse);
                string type = jsonResult["type"]?.ToString();

                // KỊCH BẢN: XỬ LÝ DANH SÁCH LỆNH (MULTI-ACTION)
                if (type == "multi_action")
                {
                    JArray actions = (JArray)jsonResult["actions"];

                    // Dòng này cực kỳ quan trọng để kiểm tra số lượng lệnh thực tế từ Python trả về!
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n[INFO]: System nhận được {actions.Count} hành động từ AI Agent.");
                    Console.ResetColor();

                    foreach (JObject actionItem in actions)
                    {
                        await ExecuteAction(actionItem, tiaEngine);
                    }
                }
                // KỊCH BẢN CŨ: ĐƠN LỆNH
                else if (type == "agent_action")
                {
                    // 🌟 SỬA QUAN TRỌNG: Thêm await ở đây luôn
                    await ExecuteAction(jsonResult, tiaEngine);
                }
                else if (type == "chat_response")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n🤖 [AI Agent]: {jsonResult["content"]}");
                    Console.ResetColor();
                }
                else if (jsonResult["status"]?.ToString() == "error")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n⚠️ [BACKEND ERROR]: {jsonResult["message"]}");
                    Console.ResetColor();
                }

            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n⚠️ [PARSE ERROR]: {ex.Message}");
                Console.ResetColor();
            }
        }
        private static async Task ExecuteAction(JObject actionItem, TIA_V20 tiaEngine)
        {
            string action = actionItem["action"]?.ToString();
            string generatedDir = GetGeneratedFilesDirectory();
            Func<string, string[]> GetValidPaths = (rawNames) =>
            {
                return rawNames.Split(',').Select(f => f.Trim())
                    .Select(f => f.EndsWith(".scl") ? f : f + ".scl")
                    .Select(f => Path.Combine(generatedDir, f))
                    .Where(p => File.Exists(p)).ToArray();
            };
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n⚙️ [SYSTEM]: Đang thực hiện -> [{action}]");
            Console.ResetColor();

            switch (action)
            {
                case "CREATE_PROJECT":
                    string namec = actionItem["name"]?.ToString();
                    string pathc = SanitizePath(actionItem["path"]?.ToString());

                    Console.WriteLine($"[i] Processing path: {pathc}");

                    if (tiaEngine.CreateTIAproject(pathc, namec, true))
                        Console.WriteLine($"✅ [SUCCESS] {action} {namec} thành công!");
                    else
                        Console.WriteLine($"❌ [FAILED] Không thể {action}.");
                    break;

                case "OPEN_PROJECT":
                    string nameo = actionItem["name"]?.ToString();
                    string patho = SanitizePath(actionItem["path"]?.ToString());
                    Console.WriteLine($"[i] Processing path: {patho}");

                    if (tiaEngine.CreateTIAproject(patho, nameo, false))
                        Console.WriteLine($"✅ [SUCCESS] {action} {nameo} thành công!");
                    else
                        Console.WriteLine($"❌ [FAILED] Không thể {action}.");
                    break;

                case "CONNECT_TIA":
                    if (tiaEngine.ConnectToTIA()) Console.WriteLine("✅ Đã kết nối TIA.");
                    else Console.WriteLine("❌ Không tìm thấy TIA.");
                    break;

                case "SAVE_PROJECT":
                    if (tiaEngine.SaveProject()) Console.WriteLine("✅ Dự án đã lưu.");
                    break;

                case "CLOSE_TIA":
                    tiaEngine.CloseTIA();
                    Console.WriteLine("✅ TIA đã đóng.");
                    break;

                case "CREATE_DEVICE":
                    string dName = actionItem["name"]?.ToString();
                    string ip = actionItem["ip"]?.ToString();
                    string mName = actionItem["model_name"]?.ToString();
                    string versionReq = actionItem.ContainsKey("version") ? actionItem["version"]?.ToString() : null;

                    string tid = LookUpInPlcCatalog(mName, versionReq);

                    if (!string.IsNullOrEmpty(tid))
                    {
                        try
                        {
                            Console.WriteLine($"🔍 [INFO]: Found TID '{tid}'. Creating '{dName}' with IP: {ip}.");

                            // Chạy bất đồng bộ
                            await Task.Run(() => tiaEngine.CreateDev(dName, tid, ip));

                            // Đợi TIA Portal phản hồi
                            await Task.Delay(2000);

                            // Kiểm tra kết quả
                            var devs = tiaEngine.GetPlcList();
                            if (devs.Any(d => d.Equals(dName, StringComparison.OrdinalIgnoreCase)))
                            {
                                SetCurrentDevice(dName, tiaEngine);
                                Console.WriteLine($"✅ [SUCCESS]: Thiết bị {dName} đã khởi tạo.");
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ [WARNING]: Lệnh đã gửi nhưng thiết bị chưa xuất hiện!");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ [CREATE DEVICE FAILED]: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ [CATALOG ERROR]: Không tìm thấy Model '{mName}' hoặc Version '{versionReq}' không khớp!");
                    }
                    break;

                case "CHOOSE_DEVICE":
                    string targetName = actionItem["name"]?.ToString();

                    Console.WriteLine($"🔍 [INFO]: Hệ thống đang tiến hành chuyển đổi ngữ cảnh sang thiết bị: '{targetName}'...");

                    bool chooseSuccess = SetCurrentDevice(targetName, tiaEngine);

                    if (chooseSuccess)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ [SUCCESS]: Đã chuyển ngữ cảnh làm việc thành công!");
                        Console.WriteLine($"👉 Thiết bị hiện tại: {Program._currentDeviceName} | IP cấu hình: {Program._currentIp}");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [FAILED]: Không tìm thấy thiết bị nào khớp với từ khóa '{targetName}' trong cấu trúc dự án TIA Portal.");
                    }
                    Console.ResetColor();
                    break;

                case "GENERATE_CODE":
                    string blocType = actionItem["block_type"]?.ToString();
                    string codQuery = actionItem["query"]?.ToString();
                    Program.CheckCapstoneMode(codQuery);
                    await HandleChatAsync(blocType, codQuery, Program._currentSessionId);
                    break;

                case "IMPORT_FB_FC":
                    string bType = actionItem["block_type"]?.ToString();
                    string[] fbPaths = GetValidPaths(actionItem["file_names"]?.ToString());
                    if (fbPaths.Length > 0) Program.TiaImportLogic(bType.ToUpper(), fbPaths);
                    break;

                case "IMPORT_OB":
                    string[] obPaths = GetValidPaths(actionItem["file_names"]?.ToString());
                    if (obPaths.Length > 0) Program.TiaOBImportLogic("OB", obPaths);
                    break;

                case "IMPORT_PLC_TAGS":
                    string rawFileNames = actionItem["file_names"]?.ToString();
                    string generated_Dir = GetGeneratedFilesDirectory();

                    // 🌟 KIỂM TRA NGỮ CẢNH: Đảm bảo đã chọn thiết bị
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Bạn chưa chọn thiết bị (PLC/SCADA) nào để nạp Tags. Hãy dùng 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    string[] fileList = rawFileNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var f in fileList)
                    {
                        string fileName = f.Trim();
                        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                            fileName += ".csv";

                        string fullPath = Path.Combine(generated_Dir, fileName);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                // Gọi API của Siemens
                                tiaEngine.ImportPlcTagsFromCsv(Program._currentDeviceName, fullPath);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ [SUCCESS]: Đã nạp Tags từ {fileName} vào thiết bị {Program._currentDeviceName}");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                // Log chi tiết lỗi từ Siemens API để debug
                                Console.WriteLine($"❌ [ERROR]: Lỗi Siemens Openness khi nạp {fileName}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠️ [WARNING]: Không tìm thấy file: {fileName} tại đường dẫn: {fullPath}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case "DRAW_SCADA":
                    string rawJsonFiles = actionItem["file_names"]?.ToString();
                    string scadaDir = GetGeneratedFilesDirectory(); // Hàm lấy thư mục gốc dự án mà bạn đã viết

                    // Kiểm tra xem đã chọn thiết bị (PC Station / HMI Panel) để vẽ giao diện lên chưa
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Chưa chọn thiết bị đích (HMI/SCADA) để vẽ giao diện. Hãy dùng 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    // Tách danh sách file bằng dấu phẩy
                    string[] jsonFileList = rawJsonFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var f in jsonFileList)
                    {
                        string fileName = f.Trim();
                        // Tự động bù đuôi .json nếu người dùng quên gõ
                        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            fileName += ".json";

                        string fullPath = Path.Combine(scadaDir, fileName);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"⚙️ [SYSTEM]: Đang tiến hành vẽ màn hình từ tệp: {fileName}...");
                                Console.ResetColor();

                                // Đọc nội dung file JSON và Deserialize cấu trúc đối tượng đồ họa
                                string jsonContent = File.ReadAllText(fullPath);
                                var jsonScreen = JsonConvert.DeserializeObject<ScadaProjectModel>(jsonContent);

                                // Gọi API động cơ TIA Portal Openness của bạn để tạo màn hình
                                tiaEngine.GenerateScadaProject(jsonScreen, Program._currentDeviceName);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ [SUCCESS]: Đã vẽ hoàn tất giao diện từ file {fileName} vào {Program._currentDeviceName}");
                                Console.ResetColor();
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ [DRAWING ERROR] Lỗi khi xử lý file {fileName}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠️ [WARNING]: Không tìm thấy file đồ họa: {fileName} tại đường dẫn: {fullPath}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case "IMPORT_HMI_TAGS":
                    string rawHmiFiles = actionItem["file_names"]?.ToString();
                    string hmiTagDir = GetGeneratedFilesDirectory(); // Lấy đường dẫn gốc thư mục chuẩn

                    // 🌟 KIỂM TRA NGỮ CẢNH: Đảm bảo trạm SCADA/HMI đã được chọn trước
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Bạn chưa chọn trạm HMI/SCADA đích để nạp Tags. Hãy dùng 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    // Tách danh sách file bằng dấu phẩy
                    string[] hmiFileList = rawHmiFiles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var f in hmiFileList)
                    {
                        string fileName = f.Trim();
                        // Tự động bù đuôi .csv nếu AI hoặc người dùng viết thiếu
                        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                            fileName += ".csv";

                        string fullPath = Path.Combine(hmiTagDir, fileName);

                        if (File.Exists(fullPath))
                        {
                            try
                            {
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine($"⚙️ [SYSTEM]: Đang tiến hành nạp bảng biến HMI từ tệp: {fileName} vào {Program._currentDeviceName}...");
                                Console.ResetColor();

                                // Gọi trực tiếp API TIA Portal Openness xử lý tên gộp của bạn
                                tiaEngine.ImportHmiTagsFromCsv(Program._currentDeviceName, fullPath);
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ [HMI TAG ERROR] Lỗi khi nạp file {fileName}: {ex.Message}");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠️ [WARNING]: Không tìm thấy file dữ liệu: {fileName} tại đường dẫn: {fullPath}");
                            Console.ResetColor();
                        }
                    }
                    break;

                case "CREATE_HMI_CONNECTION":
                    string driver = GetStandardDriverName(actionItem["driver"]?.ToString());
                    string hmiIpAddr = actionItem["hmi_ip"]?.ToString() ?? "192.168.0.2";
                    string plcIpAddr = actionItem["plc_ip"]?.ToString() ?? "192.168.0.1";
                    string ap = actionItem["access_point"]?.ToString() ?? "S7ONLINE";

                    // KIỂM TRA NGỮ CẢNH: Phải chọn thiết bị trước
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Chưa chọn trạm HMI/SCADA để thiết lập kết nối. Hãy dùng 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n⚙️ [SYSTEM]: Đang tiến hành tạo kết nối truyền thông động cho [{Program._currentDeviceName}]...");
                        Console.WriteLine($"👉 Cấu hình nạp: Driver={driver} | HMI_IP={hmiIpAddr} <-> PLC_IP={plcIpAddr} | AccessPoint={ap}");
                        Console.ResetColor();

                        // Gọi động cơ xử lý dynamic WinCC Unified Openness của bạn
                        string connectionResult = tiaEngine.CreateUnifiedConnectionDynamic(
                            Program._currentDeviceName,
                            driver,
                            hmiIpAddr,
                            plcIpAddr,
                            ap
                        );

                        if (connectionResult.StartsWith("[ERROR]"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ {connectionResult}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✅ [SUCCESS]: Khởi tạo dòng kết nối thành công -> Tên kết nối: [{connectionResult}]");
                            Console.ResetColor();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [CRITICAL CONNECTION ERROR]: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "CHANGE_IP":
                    string targetIp = actionItem["ip"]?.ToString();
                    string targetSubnet = actionItem["subnet"]?.ToString() ?? "255.255.255.0";
                    string targetGateway = actionItem["gateway"]?.ToString() ?? "";

                    // 🌟 KHÓA SHIELD BẢO VỆ NGỮ CẢNH: Kiểm tra thiết bị hiện hành
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Bạn chưa chọn thiết bị nào để thực hiện cấu hình mạng! Chuỗi lệnh CHANGE_IP bị từ chối.");
                        Console.WriteLine("👉 Vui lòng sử dụng lệnh 'CHOOSE_DEVICE' trước hoặc chỉ định tên thiết bị cụ thể trong câu lệnh.");
                        Console.ResetColor();
                        break;
                    }

                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n⚙️ [SYSTEM]: Đang thực hiện thay đổi cấu hình IP Address cho thiết bị hiện hành: [{Program._currentDeviceName}]...");
                        Console.WriteLine($"👉 Thông số mạng mới -> IP: {targetIp} | Subnet: {targetSubnet} | GW: {(string.IsNullOrEmpty(targetGateway) ? "None" : targetGateway)}");
                        Console.ResetColor();

                        // Gọi trực tiếp động cơ phần cứng Siemens Openness của bạn
                        string ipUpdateResult = tiaEngine.UpdateNetworkSettings(
                            Program._currentDeviceName,
                            targetIp,
                            targetSubnet,
                            targetGateway
                        );

                        if (ipUpdateResult.Contains("SUCCESS"))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✅ {ipUpdateResult}");

                            // CẬP NHẬT LẠI BIẾN BIẾN TOÀN CỤC: Đảm bảo IP mới được đồng bộ lên giao diện CLI
                            Program._currentIp = targetIp;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ {ipUpdateResult}");
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [NETWORK ENGINE CRASH]: Lỗi hệ thống khi cập nhật IP: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "COMPILE":
                    string mode = actionItem["mode"]?.ToString() ?? "both";
                    bool rebuildAttr = actionItem["rebuild"]?.ToObject<bool>() ?? false;

                    // KIỂM TRA NGỮ CẢNH: Bắt buộc phải chọn thiết bị trước khi bấm nút Compile
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Bạn chưa chọn thiết bị nào để biên dịch. Hãy dùng lệnh 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(rebuildAttr
                            ? $"\n⚙️ [SYSTEM]: Đang thực hiện REBUILD toàn bộ cấu trúc thiết bị: [{Program._currentDeviceName}]..."
                            : $"\n⚙️ [SYSTEM]: Đang tiến hành COMPILE thiết bị hiện hành: [{Program._currentDeviceName}]...");
                        Console.WriteLine($"👉 Chế độ: Mode={mode.ToUpper()} | Rebuild All={rebuildAttr}");
                        Console.ResetColor();

                        // Phân tích logic chế độ cứng/mềm từ chuỗi JSON của AI gửi về
                        bool compileHw = (mode == "hw" || mode == "both");
                        bool compileSw = (mode == "sw" || mode == "both");

                        // Gọi trực tiếp động cơ biên dịch Siemens Openness của bạn
                        // Vì hàm Compile có thể tốn thời gian chạy nền của TIA, ta bọc Task.Run chạy bất đồng bộ cho mượt
                        string compileResult = await Task.Run(() =>
                            tiaEngine.CompileSpecific(Program._currentDeviceName, compileHw, compileSw, rebuildAttr)
                        );

                        // In kết quả trả về từ TIA Portal ra màn hình Console
                        if (compileResult.Contains("FAILED") || compileResult.Contains("Error"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ [COMPILE FAILED]: {compileResult}");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✅ [COMPILE COMPLETED]: {compileResult}");
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [COMPILE ENGINE ERROR]: Lỗi tiến trình biên dịch Openness: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "ADD_MODULE":
                    string moduleModel = actionItem["model_name"]?.ToString();
                    int targetSlot = actionItem["slot"]?.ToObject<int>() ?? 2;

                    // 🌟 SHIELD BẢO VỆ NGỮ CẢNH: Bắt buộc phải chọn PLC đích trước
                    if (string.IsNullOrEmpty(Program._currentDeviceName) || Program._currentDeviceName == "None")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("❌ [ERROR]: Bạn chưa chọn PLC nào để cắm Module mở rộng. Hãy dùng 'CHOOSE_DEVICE' trước!");
                        Console.ResetColor();
                        break;
                    }

                    try
                    {
                        // Dò tìm dòng họ PLC (S71200 hoặc S71500) để tra cứu chính xác mảng dữ liệu trong JSON
                        string plcFamily = tiaEngine.GetDeviceFamily(Program._currentDeviceName);

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"\n⚙️ [SYSTEM]: Đang tiến hành phân tích và gắn module [{moduleModel}] vào Slot [{targetSlot}] của thiết bị [{Program._currentDeviceName}] ({plcFamily})...");
                        Console.ResetColor();

                        // Gọi bộ dò Catalog tự động
                        string computedIdentifier = LookUpModuleInCatalog(moduleModel, plcFamily);
                        if (string.IsNullOrEmpty(plcFamily) || plcFamily.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
                        {
                            // Nếu thiết bị chứa mã 531-7NF (như trong log của bạn), tự động hiểu là họ S7-1500
                            plcFamily = moduleModel.Contains("531-") || Program._currentDeviceName.Contains("1500") ? "S71500" : "S71200";
                        }
                        // Nếu tìm không thấy trong Catalog, kiểm tra xem AI có truyền thẳng mã MLFB bằng tay không
                        if (string.IsNullOrEmpty(computedIdentifier))
                        {
                            // Nếu chuỗi nhập vào có định dạng giống mã Order Number (Chứa ký tự hoặc độ dài chuẩn)
                            if (moduleModel.StartsWith("6ES7") || moduleModel.StartsWith("6GK7"))
                            {
                                computedIdentifier = $"OrderNumber:{moduleModel}/V2.2"; // Gán firmware mặc định dự phòng
                                Console.WriteLine($"🔍 [INFO]: Không tìm thấy chuỗi '{moduleModel}' trong Catalog, chuyển sang chế độ nạp thủ công mã MLFB.");
                            }
                        }

                        if (!string.IsNullOrEmpty(computedIdentifier))
                        {
                            Console.WriteLine($"🔍 [INFO]: Đúc mã định danh Openness thành công: {computedIdentifier}");

                            // Gọi trực tiếp hàm Plug vật lý bất đồng bộ của bạn xuống Rack Openness
                            string plugResult = await Task.Run(() => tiaEngine.PlugModule(Program._currentDeviceName, computedIdentifier, targetSlot)
                            );

                            if (plugResult.Contains("SUCCESS"))
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✅ {plugResult}");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ {plugResult}");
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"❌ [CATALOG ERROR]: Không tìm thấy Module nào khớp với mô tả '{moduleModel}' trong tệp ModuleCatalog.json.");
                            Console.ResetColor();
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ [HARDWARE CONFIG CRASH]: Lỗi tiến trình cấu hình phần cứng: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;
            }
        }

        private static string LookUpInPlcCatalog(string modelName, string requestedVersion)
        {
            string catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcCatalog.json");
            if (!File.Exists(catalogPath)) return null;

            try
            {
                string jsonContent = File.ReadAllText(catalogPath);
                JObject catalog = JObject.Parse(jsonContent);

                // Chuẩn hóa từ khóa tìm kiếm (bỏ khoảng trắng, dấu gạch ngang)
                string normalizedInput = modelName.ToLower().Replace(" ", "").Replace("-", "");

                List<JObject> matches = new List<JObject>();

                // 1. Thu thập tất cả các thiết bị có tên khớp với từ khóa trong mọi danh mục
                foreach (var property in catalog.Properties())
                {
                    JArray devices = (JArray)property.Value;
                    foreach (var device in devices)
                    {
                        string rawName = device["Name"]?.ToString() ?? "";
                        string normalizedJsonName = rawName.ToLower().Replace(" ", "").Replace("-", "");

                        if (normalizedJsonName.Contains(normalizedInput))
                        {
                            matches.Add((JObject)device);
                        }
                    }
                }

                if (matches.Count == 0) return null;

                // 2. Logic ưu tiên: Nếu người dùng yêu cầu version cụ thể (vd: 4.5)
                if (!string.IsNullOrEmpty(requestedVersion))
                {
                    string v = requestedVersion.ToUpper().StartsWith("V") ? requestedVersion : "V" + requestedVersion;

                    // Tìm thiết bị nào trong các kết quả khớp có hỗ trợ phiên bản này
                    var perfectMatch = matches.FirstOrDefault(m =>
                        ((JArray)m["AvailableVersions"]).Any(ver => ver.ToString() == v));

                    if (perfectMatch != null)
                    {
                        return $"OrderNumber:{perfectMatch["OrderNumber"]}/{v}";
                    }
                }

                // 3. Nếu không có yêu cầu version hoặc không tìm thấy bản khớp hoàn hảo,
                // chọn thiết bị có Version mới nhất (cao nhất) trong số các kết quả khớp
                var bestDevice = matches.OrderByDescending(m => m["Version"].ToString()).FirstOrDefault();

                if (bestDevice != null)
                {
                    string order = bestDevice["OrderNumber"]?.ToString();
                    string ver = bestDevice["Version"]?.ToString();
                    // Đảm bảo version có tiền tố V
                    if (!ver.ToUpper().StartsWith("V")) ver = "V" + ver;

                    return $"OrderNumber:{order}/{ver.ToUpper()}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [PLC CATALOG ERROR]: {ex.Message}");
            }
            return null;
        }

        private static string LookUpModuleInCatalog(string modelName, string family)
        {
            string catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModuleCatalog.json");
            if (!File.Exists(catalogPath)) return null;

            try
            {
                string jsonContent = File.ReadAllText(catalogPath);
                JObject catalog = JObject.Parse(jsonContent);

                // Chuẩn hóa tên đầu vào từ AI để tăng tỉ lệ khớp (Xóa khoảng trắng, chữ thường)
                string normalizedInput = modelName.ToLower().Replace(" ", "").Replace("-", "");

                // Xác định mảng module cần quét dựa trên Family của PLC đang chọn (S71200 hoặc S71500)
                string arrayKey = (family == "S71200") ? "S71200_Modules" : "S71500_Modules";
                if (!catalog.ContainsKey(arrayKey)) return null;

                JArray modules = (JArray)catalog[arrayKey];
                foreach (var mod in modules)
                {
                    string rawName = mod["Name"]?.ToString() ?? "";
                    string orderNum = mod["OrderNumber"]?.ToString() ?? "";
                    string version = mod["Version"]?.ToString() ?? "V1.0";

                    string normalizedName = rawName.ToLower().Replace(" ", "").Replace("-", "");
                    string normalizedOrder = orderNum.ToLower().Replace(" ", "").Replace("-", "");

                    // Khớp thông minh: Kiểm tra nếu chuỗi AI gửi về trùng tên hoặc chứa mã Part Number
                    if (normalizedName.Contains(normalizedInput) || normalizedOrder.Contains(normalizedInput) || normalizedInput.Contains(normalizedOrder))
                    {
                        return $"OrderNumber:{orderNum}/{version}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [CATALOG LOG ERROR]: Lỗi đọc ModuleCatalog.json: {ex.Message}");
            }
            return null;
        }

        private static bool SetCurrentDevice(string devName, TIA_V20 tiaEngine)
        {
            if (string.IsNullOrWhiteSpace(devName)) return false;

            // Lấy danh sách toàn bộ tên thiết bị (đã qua xử lý gộp tên từ GetPlcList)
            var devs = tiaEngine.GetPlcList();
            if (devs == null || devs.Count == 0) return false;

            // Tìm kiếm thông minh: Chấp nhận khớp hoàn toàn hoặc khớp một phần (chứa từ khóa)
            // Ví dụ: người dùng nhập "Syrup_scada" vẫn khớp với "Syrup_scada|HMI_RT_1"
            string matchedDevice = devs.FirstOrDefault(d =>
                d.Equals(devName, StringComparison.OrdinalIgnoreCase) ||
                d.IndexOf(devName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (matchedDevice != null)
            {
                // Cập nhật trạng thái hệ thống CLI toàn cục
                Program._currentDeviceName = matchedDevice;

                // Truy vấn địa chỉ IP thực tế từ API phần cứng của Siemens
                Program._currentIp = tiaEngine.GetDeviceIp(matchedDevice);

                return true;
            }

            return false;
        }

        private static string GetGeneratedFilesDirectory()
        {
            // Lấy thư mục chứa file .exe đang chạy
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // Kết hợp trực tiếp để tạo folder "Generated_Files" nằm ngay trong folder đó
            string generatedDir = Path.Combine(baseDir, "Generated_Files");

            // Kiểm tra và tạo folder nếu chưa tồn tại
            if (!Directory.Exists(generatedDir))
            {
                try
                {
                    Directory.CreateDirectory(generatedDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [ERROR]: Không thể tạo thư mục tại {generatedDir}. Lỗi: {ex.Message}");
                    // Nếu không thể tạo thư mục, trả về thư mục hiện tại để tránh crash chương trình
                    return baseDir;
                }
            }

            return generatedDir;
        }

        private static string SanitizePath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) return string.Empty;

            // 1. Loại bỏ các dấu gạch chéo đôi (do AI sinh ra dư thừa)
            // 2. Tự động chuyển đổi định dạng hệ thống sang chuẩn của Windows
            string cleanPath = inputPath.Replace("\\\\", "\\").Replace("/", "\\");

            // 3. Sử dụng Path.GetFullPath để loại bỏ các ký tự lạ và chuẩn hóa về dạng đầy đủ
            try
            {
                return Path.GetFullPath(cleanPath);
            }
            catch
            {
                return cleanPath; // Trả về đường dẫn gốc nếu Path bị lỗi định dạng nghiêm trọng
            }
        }

        private static string GetStandardDriverName(string inputDriver)
        {
            if (string.IsNullOrWhiteSpace(inputDriver))
                return "SIMATIC S7 1200/1500";

            string normalized = inputDriver.ToLower().Replace(" ", "").Replace("-", "");

            // Ép driver về danh mục chuẩn
            switch (normalized)
            {
                // Nếu AI trả về tên này, hãy chấp nhận nó như một alias hợp lệ
                case var s when s.Contains("s71200") || s.Contains("s71500") || s == "simatics71500":
                    return "SIMATIC S7 1200/1500";

                case var m when m.Contains("modbus"):
                    return "Modbus TCP";

                default:
                    // Nếu không khớp, ghi log để bạn kiểm tra xem AI đang "ngáo" tên nào
                    Console.WriteLine($"⚠️ [DEBUG]: AI trả về driver lạ: {inputDriver}");
                    return "SIMATIC S7 1200/1500";
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        public static async Task HandleLoadTagsAsync(string tagFilePath)
        {
            Console.WriteLine($"\n🚀 [START] Starting to load I/O Tags from: {tagFilePath}");

            if (string.IsNullOrEmpty(tagFilePath) || !File.Exists(tagFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Tag file not found. Please check the path again.");
                Console.ResetColor();
                return;
            }

            string userTagsContent = "";
            string ext = Path.GetExtension(tagFilePath).ToLower();

            if (ext == ".xlsx" || ext == ".xls") userTagsContent = TagManager.ReadUserTagsExcel(tagFilePath);
            else if (ext == ".csv") userTagsContent = TagManager.ReadUserTagsCsv(tagFilePath);

            if (!string.IsNullOrEmpty(userTagsContent))
            {
                // Ghi đè chuỗi Tag đã gọt sạch sẽ vào file txt ẩn
                File.WriteAllText(TagCacheFile, userTagsContent);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[SUCCESS] Successfully saved Tags to local cache!");
                Console.ResetColor();
            }
        }

        public static async Task HandleChatAsync(string targetType, string query, string sessionId)
        {
            var settings = SettingsManager.Load();
            string keytoPass = "";

            if (settings.Mode == "USER")
            {
                if (settings.IsKeyMissing)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[API ERROR] You are in USER MODE but no API Key is configured.");
                    Console.WriteLine("Please type the command 'config' to enter your Gemini API Key.");
                    Console.ResetColor();
                    return; // CHẶN LẠI NGAY LẬP TỨC, không gọi AI nữa
                }
                keytoPass = settings.UserApiKey; // Lấy key của User để gửi
            }
            else if (settings.Mode == "DEV")
            {
                keytoPass = ""; // Bản DEV sẽ dùng key cứng trong code Python, không gửi gì cả
            }

            Console.WriteLine($"\n🚀 [START] Generating code for block: {targetType}");
            //Console.WriteLine($"[INFO] Key using: {keytoPass}");

            string userTagsContent = "";

            // Nếu là OB, HMI hoặc CWC, tự động đi mò file Cache xem có tồn tại không
            if (targetType == "ORGANIZATION_BLOCK" || targetType == "HMI_SCREEN" || targetType == "CWC_SCREEN")
            {
                if (File.Exists(TagCacheFile))
                {
                    byte[] fileBytes = File.ReadAllBytes(TagCacheFile);
                    string content = Encoding.UTF8.GetString(fileBytes);
                    userTagsContent = content.TrimStart('\uFEFF');
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[SYSTEM] Found I/O Tags in Cache. Proceeding to attach to Prompt.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[WARNING] No info about Tag Cache. (Not yet uploaded via 'load-tags'). AI will generate new tags.");
                    Console.ResetColor();
                }
            }

            // Gọi backend
            var backendTask = AiEngine.CallPythonBackendAsync(query, sessionId, "chat", "", "", targetType, userTagsContent, settings.Mode, keytoPass);
            string jsonResponse = await RunWithSpinner(backendTask, "Generating...");

            if (string.IsNullOrWhiteSpace(jsonResponse))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[CRITICAL ERROR]: Python backend did not return any response. Please check the Python server logs for more details.");
                Console.ResetColor();
                return;
            }
            ProcessResponse(jsonResponse);
            Console.WriteLine($"\n [DONE] Code generation completed!\n");
        }

        public static async Task HandleLoadSpecAsync(string specPath, string sessionId)
        {
            Console.WriteLine($"\n🚀 [START] Loading operational requirements into Vector DB...");
            if (!File.Exists(specPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Spec file not found: {specPath}");
                Console.ResetColor();
                return;
            }

            // --- BẮT ĐẦU: ĐỌC VÀ CHẮT LỌC TEXT TỪ FILE ---
            string specText = "";
            if (specPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                // Nếu là file Word, dùng dao mổ XML để lấy chữ và bảng
                specText = ExtractTextFromDocx(specPath);
            }
            else
            {
                // Nếu là file txt, md, scl thông thường
                string rawSpec = File.ReadAllText(specPath, Encoding.UTF8);
                specText = rawSpec.TrimStart('\uFEFF');
            }
            // --- KẾT THÚC: ĐỌC FILE ---

            if (specText.StartsWith("[ERROR]"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(specText);
                Console.ResetColor();
                return;
            }

            // Gửi toàn bộ text sạch sẽ sang Python để băm nhỏ và nạp vào ChromaDB
            var backendTask = AiEngine.CallPythonBackendAsync("", sessionId, "update_spec", "", specText);
            string jsonResponse = await RunWithSpinner(backendTask, "Chunking and uploading to ChromaDB...");

            try
            {
                dynamic obj = JsonConvert.DeserializeObject(jsonResponse);
                if (obj.status == "success") { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"[SUCCESS] {obj.message}"); }
                else { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"[ERROR] {obj.message}"); }
            }
            catch { Console.WriteLine("[ERROR] Error parsing response from Python Backend."); }
            Console.ResetColor();
        }

        public static async Task HandleClearDataAsync(string sessionId)
        {
            // --- WARNING SHIELD ---
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" [HIGH LEVEL WARNING] You are about to delete all knowledge (Spec & Tags) of this project!");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("This action CANNOT be undone. Are you absolutely sure? (y/n): ");
            Console.ResetColor();

            string confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm != "y" && confirm != "yes")
            {
                Program.PrintIcon("i", "Cleanup cancelled. Data is still safe.", ConsoleColor.DarkGray);
                return;
            }

            // --- START DELETION ---
            Console.WriteLine($"\n🚀 [START] Proceeding with system cleanup...");

            // Đấm 1: Xóa file Tag Cache
            if (File.Exists(TagCacheFile))
            {
                try
                {
                    File.Delete(TagCacheFile);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[SUCCESS] Successfully deleted all I/O Tags from Cache.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Cannot delete Tag Cache: {ex.Message}");
                }
                Console.ResetColor();
            }

            // Đấm 2: Xóa Vector DB
            var backendTask = AiEngine.CallPythonBackendAsync("", sessionId, "clear_spec");
            string jsonResponse = await RunWithSpinner(backendTask, "Cleaning up Vector DB...");

            try
            {
                int startIdx = jsonResponse.IndexOf('{');
                int endIdx = jsonResponse.LastIndexOf('}');
                if (startIdx != -1 && endIdx != -1 && endIdx >= startIdx)
                {
                    jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
                }
                dynamic obj = JsonConvert.DeserializeObject(jsonResponse);

                if (obj.status == "success")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] {obj.message}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] {obj.message}");
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Error parsing response from Python Backend.");
            }

            Console.ResetColor();
        }

        public static async Task HandleCheckStatusAsync(string sessionId)
        {
            // 1. Quét I/O Tags (Bộ nhớ cục bộ C#)
            string tagStatus = "❌ NOT LOADED (Empty)";
            string tagFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags_cache.txt");

            if (File.Exists(tagFilePath))
            {
                FileInfo fi = new FileInfo(tagFilePath);
                tagStatus = $"✅ LOADED | Size: {fi.Length / 1024} KB | Updated: {fi.LastWriteTime:dd/MM/yyyy HH:mm}";
            }

            // 2. Quét System Spec (Gọi sang Vector DB của Python)
            string specStatus = "QUERYING...";
            try
            {
                // Gọi API backend với Spinner
                var backendTask = AiEngine.CallPythonBackendAsync("", sessionId, "check_spec");
                string jsonResponse = await RunWithSpinner(backendTask, $"Scanning vector DB for session [{sessionId.ToUpper()}]...");

                dynamic obj = JsonConvert.DeserializeObject(jsonResponse);
                if (obj.status == "success")
                {
                    string msg = obj.message.ToString();

                    // BỘ LỌC THÔNG MINH: Đọc hiểu câu trả lời của Python
                    if (msg.StartsWith("No current"))
                    {
                        specStatus = "❌ Empty (No Spec documents loaded into Vector DB)";
                    }
                    else
                    {
                        string briefMsg = msg.Split('\n')[0];
                        specStatus = $"✅ Loaded | {briefMsg} | Status: Ready";
                    }
                }
                else
                {
                    specStatus = $"❌ ERROR FROM PYTHON: {obj.message}";
                }
            }
            catch (Exception)
            {
                specStatus = "❌ ERROR CONNECTING TO PYTHON BACKEND";
            }

            // 3. DỌN SẠCH MÀN HÌNH (XÓA SPINNER) & VẼ UI DASHBOARD
            Console.Clear();
            Console.WriteLine("\n" + new string('=', 70));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($" 📊 DATA STATUS - SESSION: {sessionId.ToUpper()}");
            Console.ResetColor();
            Console.WriteLine(new string('=', 70));

            Console.Write(" [1] I/O Tags (Cache)   : ");
            Console.ForegroundColor = tagStatus.Contains("✅") ? ConsoleColor.Green : ConsoleColor.DarkGray;
            Console.WriteLine(tagStatus);
            Console.ResetColor();

            Console.Write(" [2] System Spec (RAG)  : ");
            Console.ForegroundColor = specStatus.Contains("✅") ? ConsoleColor.Green : ConsoleColor.DarkGray;
            Console.WriteLine(specStatus);
            Console.ResetColor();

            Console.WriteLine(new string('=', 70) + "\n");
        }

        public static async Task HandleSessionMenuAsync()
        {
            bool keepMenuOpen = true;

            while (keepMenuOpen)
            {
                List<string> dbSessions = new List<string>();
                try
                {
                    var backendTask = AiEngine.CallPythonBackendAsync("", Program._currentSessionId, "list_sessions");

                    // Spinner sẽ xoay ở màn hình cũ, tải xong sẽ bị Console.Clear() quét sạch!
                    string jsonRes = await RunWithSpinner(backendTask, "Synchronizing session list...", 100);
                    //Console.WriteLine("DEBUG: Raw backend response: " + jsonRes);

                    int jsonStart = jsonRes.IndexOf('{');
                    int jsonEnd = jsonRes.LastIndexOf('}');

                    if (jsonStart >= 0 && jsonEnd >= jsonStart)
                    {
                        string cleanJson = jsonRes.Substring(jsonStart, jsonEnd - jsonStart + 1);
                        dynamic obj = JsonConvert.DeserializeObject(cleanJson);

                        if (obj != null && obj.status == "success" && obj.sessions != null)
                        {
                            foreach (var s in obj.sessions)
                            {
                                dbSessions.Add((string)s);
                            }
                            //Console.WriteLine("DEBUG: Sessions from backend: " + string.Join(", ", dbSessions));
                        }
                    }
                    else
                    {
                        // Mở khiên kiểm tra: Nếu Python không trả về JSON, in ra để xem nó trả về cái gì
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n[WARNING]: Backend return invalid JSON. Raw data: {jsonRes}");
                        Console.ResetColor();
                        await Task.Delay(3000); // Dừng 3 giây cho lập trình viên đọc lỗi
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR C# PARSE JSON]: {ex.Message}");
                    Console.ResetColor();
                    await Task.Delay(2000);
                }

                if (!dbSessions.Contains("default")) dbSessions.Insert(0, "default");
                if (!dbSessions.Contains(Program._currentSessionId)) dbSessions.Add(Program._currentSessionId);

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("==========================================================");
                Console.WriteLine("            CHAT SESSION MENU (CHAT & CONTEXT)");
                Console.WriteLine("==========================================================");
                Console.ResetColor();


                Console.WriteLine($"\n Current session: [{Program._currentSessionId.ToUpper()}]\n");

                // --- IN DANH SÁCH ---
                for (int i = 0; i < dbSessions.Count; i++)
                {
                    if (dbSessions[i] == Program._currentSessionId)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [{i + 1}] -> {dbSessions[i]} (Active)");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"  [{i + 1}]    {dbSessions[i]}");
                    }
                }

                Console.WriteLine("\n----------------------------------------------------------");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" [NUMBER]: Choose | [C]: Create Session | [S]: Delete Session | [H]: Clear Chat History | [ESC]: Exit");
                Console.ResetColor();

                // --- BẮT SỰ KIỆN BÀN PHÍM ---
                var keyInfo = Console.ReadKey(intercept: true);
                char key = char.ToUpper(keyInfo.KeyChar);

                if (keyInfo.Key == ConsoleKey.Escape)
                {
                    keepMenuOpen = false;
                    Console.Clear(); // Trả lại màn hình gõ lệnh
                }
                // TÍNH NĂNG [C]: TẠO SESSION CÓ SPINNER
                else if (key == 'C')
                {
                    Console.WriteLine("\n");
                    Console.Write(" >> New session name (No spaces): ");
                    string newSession = Console.ReadLine()?.Trim().Replace(" ", "_").ToLower();

                    if (!string.IsNullOrEmpty(newSession) && !dbSessions.Contains(newSession))
                    {
                        // GẮN SPINNER TẠO MỚI
                        var createTask = AiEngine.CallPythonBackendAsync("", newSession, "create_session");
                        await RunWithSpinner(createTask, $"Initializing space for [{newSession}]...");

                        Program._currentSessionId = newSession;
                        Program.PrintIcon("√", $"Session created and switched to: {newSession}", ConsoleColor.Green);
                        await Task.Delay(1000);
                    }
                }
                // TÍNH NĂNG [S] & [H]: XÓA DỮ LIỆU CÓ SPINNER
                else if (key == 'S' || key == 'H')
                {
                    string actionName = key == 'S' ? "DELETE SESSION" : "DELETE CHAT HISTORY";
                    Console.WriteLine($"\n\n 👉 Choose [{actionName}].");
                    Console.Write(" >> Enter the session number you want to operate on (Press Enter to Cancel): ");

                    if (int.TryParse(Console.ReadLine(), out int targetIdx) && targetIdx > 0 && targetIdx <= dbSessions.Count)
                    {
                        string targetSession = dbSessions[targetIdx - 1];

                        if (key == 'S' && targetSession == "default")
                        {
                            Program.PrintIcon("×", "CANCELLED: Cannot delete the default session.", ConsoleColor.Red);
                            await Task.Delay(1500);
                            continue;
                        }

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write($" ⚠️ WARNING: Confirm [{actionName}] with [{targetSession}]? (y/n): ");
                        Console.ResetColor();

                        string confirm = Console.ReadLine()?.Trim().ToLower();
                        if (confirm == "y" || confirm == "yes")
                        {
                            // GẮN SPINNER XÓA DỮ LIỆU
                            var resetTask = AiEngine.CallPythonBackendAsync("", targetSession, "reset");
                            await RunWithSpinner(resetTask, $"Processing cleanup for [{targetSession}]...");

                            if (key == 'S')
                            {
                                Program.PrintIcon("√", $"Successfully destroyed Session: {targetSession}", ConsoleColor.Green);
                                if (Program._currentSessionId == targetSession)
                                {
                                    Program._currentSessionId = "default";
                                    Program.PrintIcon("i", "Successfully switched to default session.", ConsoleColor.Cyan);
                                }
                            }
                            else
                            {
                                // GẮN SPINNER TẠO LẠI VỎ SESSION MỚI (CHỈ MẤT LỊCH SỬ)
                                var recreateTask = AiEngine.CallPythonBackendAsync("", targetSession, "create_session");
                                await RunWithSpinner(recreateTask, $"Initializing new session shell for [{targetSession}]...");
                                Program.PrintIcon("√", $"Successfully cleared chat history for: {targetSession}", ConsoleColor.Green);
                            }
                            await Task.Delay(1500);
                        }
                    }
                }
                // TÍNH NĂNG [SỐ]: CHỌN SESSION
                else if (int.TryParse(key.ToString(), out int selection) && selection > 0 && selection <= dbSessions.Count)
                {
                    Program._currentSessionId = dbSessions[selection - 1];
                    Program.PrintIcon("√", $"Successfully switched to Session: {Program._currentSessionId}", ConsoleColor.Green);
                    await Task.Delay(800);
                }
            }
        }

        public static async Task HandleCheckDataAsync(string sessionId)
        {
            Console.WriteLine();

            // Dùng StringBuilder để đúc một file text hoàn chỉnh
            StringBuilder dumpData = new StringBuilder();
            dumpData.AppendLine("==========================================================");
            dumpData.AppendLine($" TIA COPILOT - CONTEXT DATABASE (SESSION: {sessionId.ToUpper()})");
            dumpData.AppendLine($" EXPORTED DATE: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            dumpData.AppendLine("==========================================================\n");

            // --- 1. LẤY I/O TAGS CACHE (LOCAL C#) ---
            dumpData.AppendLine("--- [1] I/O TAGS CACHE (LOCAL) ---");
            string tagFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tags_cache.txt");
            if (File.Exists(tagFilePath))
            {
                string tagsContent = File.ReadAllText(tagFilePath);
                dumpData.AppendLine(tagsContent.TrimStart('\uFEFF'));
            }
            else
            {
                dumpData.AppendLine("(Empty - No I/O Tags loaded)");
            }
            dumpData.AppendLine("\n");

            // --- 2. LẤY SYSTEM SPEC (VECTOR DB PYTHON) ---
            dumpData.AppendLine("--- [2] SYSTEM SPEC (VECTOR DB) ---");
            try
            {
                var backendTask = AiEngine.CallPythonBackendAsync("", sessionId, "check_spec");
                string jsonResponse = await RunWithSpinner(backendTask, $" Gathering data from Vector DB for Session [{sessionId.ToUpper()}]...", 300);

                int startIdx = jsonResponse.IndexOf('{');
                int endIdx = jsonResponse.LastIndexOf('}');
                if (startIdx != -1 && endIdx != -1 && endIdx >= startIdx)
                {
                    jsonResponse = jsonResponse.Substring(startIdx, endIdx - startIdx + 1);
                }
                dynamic obj = JsonConvert.DeserializeObject(jsonResponse);

                if (obj.status == "success")
                {
                    string msg = obj.message.ToString();

                    // BỘ LỌC THÔNG MINH ĐÃ SỬA
                    if (msg.StartsWith("No current"))
                    {
                        dumpData.AppendLine("(Empty - System has not loaded any Spec documents)");
                    }
                    else
                    {
                        dumpData.AppendLine(msg); // In toàn bộ nội dung Spec ra Notepad
                    }
                }
                else
                {
                    dumpData.AppendLine($"[ERROR FROM PYTHON]: {obj.message}");
                }
            }
            catch (Exception ex)
            {
                dumpData.AppendLine($"[CONNECTION ERROR WITH PYTHON BACKEND]: {ex.Message}");
            }

            // --- 3. XUẤT FILE VÀ GỌI NOTEPAD ---
            try
            {
                string exportFileName = "TIA_Copilot_Context_Dump.txt";
                string exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exportFileName);

                File.WriteAllText(exportPath, dumpData.ToString(), Encoding.UTF8);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[√] Successfully exported to file: {exportFileName}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" Path: {exportPath}\n");
                Console.ResetColor();

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exportPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[×] Cannot automatically open Notepad. OS Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("👉 Manually open the file as the following address.");
            }
        }

        public static async Task<T> RunWithSpinner<T>(Task<T> targetTask, string waitingMessage, int timeoutSeconds = 300)
        {
            char[] spinnerChars = new char[] { '|', '/', '-', '\\' };
            int spinnerIndex = 0;

            Console.ForegroundColor = ConsoleColor.Yellow;
            // In thông báo và dư ra 1 dấu cách để làm khoảng trống cho Spinner
            Console.Write($"{waitingMessage}  ");

            DateTime timeoutTime = DateTime.Now.AddSeconds(timeoutSeconds);

            try
            {
                while (!targetTask.IsCompleted)
                {
                    if (DateTime.Now > timeoutTime)
                    {
                        throw new TimeoutException($"Backend did not respond within {timeoutSeconds} seconds.");
                    }

                    // TUYỆT KỸ: Dùng \b lùi con trỏ lại 1 ô, in ký tự mới đè lên, không xài tọa độ tuyệt đối
                    Console.Write("\b" + spinnerChars[spinnerIndex]);
                    spinnerIndex = (spinnerIndex + 1) % spinnerChars.Length;
                    await Task.Delay(100);
                }

                return await targetTask;
            }
            catch (TimeoutException ex)
            {
                if (typeof(T) == typeof(string))
                {
                    string fakeJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        message = $"TIMEOUT CRASH: {ex.Message} Please check the Python Server!"
                    });
                    return (T)(object)fakeJson;
                }
                throw;
            }
            catch (Exception ex)
            {
                if (typeof(T) == typeof(string))
                {
                    string fakeJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        message = $"CONNECTION ERROR: {ex.Message}"
                    });
                    return (T)(object)fakeJson;
                }
                throw;
            }
            finally
            {
                // Khi xong việc: lùi lại xóa cái Spinner đi và thay bằng khoảng trắng, rồi xuống dòng
                Console.Write("\b \n");
                Console.ResetColor();
            }
        }

        public static void ProcessResponse(string jsonResponse)
        {
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                try
                {
                    int startIndex = jsonResponse.IndexOf('{');
                    int endIndex = jsonResponse.LastIndexOf('}');

                    if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
                    {
                        // Cắt lấy đúng phần lõi, mọi dấu phẩy hay text dư bên ngoài sẽ bị vứt bỏ
                        jsonResponse = jsonResponse.Substring(startIndex, endIndex - startIndex + 1);
                    }

                    jsonResponse = Regex.Replace(jsonResponse, @"\}\s*,\s*""global_tags""", @", ""global_tags""");
                    jsonResponse = Regex.Replace(jsonResponse, @"\}\s*""global_tags""", @", ""global_tags""");

                    JObject responseObj = JObject.Parse(jsonResponse);

                    if (responseObj.ContainsKey("provider"))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        string provider = responseObj["provider"]?.ToString();
                        Console.WriteLine($"[AI] Using: {provider}");
                        Console.ResetColor();
                    }

                    if (responseObj.ContainsKey("token_usage"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        string inTokens = responseObj.ContainsKey("input_tokens") ? responseObj["input_tokens"].ToString() : "?";
                        string outTokens = responseObj.ContainsKey("output_tokens") ? responseObj["output_tokens"].ToString() : "?";

                        Console.WriteLine($"\n [TOKEN MONITOR] Total: {responseObj["token_usage"]} (Input: {inTokens} | Output: {outTokens})");
                        Console.ResetColor();
                    }
                    if (responseObj.ContainsKey("active_key"))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($" [API MONITOR] Active Key: {responseObj["active_key"]}");
                        Console.ResetColor();
                    }

                    if (responseObj.ContainsKey("status") && responseObj["status"].ToString() == "error")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[AI ERROR]: {responseObj["message"]}");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Raw JSON: {jsonResponse}");
                        Console.ResetColor();
                    }
                    else
                    {
                        // ROUTING GATE: Detect response type from JSON structure.
                        // CWC responses contain "cwc_info". HMI responses contain "screen_info".
                        // SCL responses contain "block_info" or "iec_61131_3_code".
                        if (responseObj.ContainsKey("cwc_info"))
                        {
                            var cwcData = CwcDataNormalizer.Normalize(responseObj);
                            CwcGenerator.GenerateAndSave(cwcData);
                        }
                        else if (responseObj.ContainsKey("screen_info"))
                        {
                            var hmiData = HmiDataNormalizer.Normalize(responseObj);
                            HmiGenerator.GenerateAndSave(hmiData);
                        }
                        else
                        {
                            var standardizedData = DataNormalizer.Normalize(responseObj);
                            SCLGenerator.GenerateAndSave(standardizedData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[ERROR C# PARSE JSON]: {ex.Message}");

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n--- RAW DATA SENT FROM AI ---");
                    Console.WriteLine(jsonResponse);
                    Console.WriteLine("---------------------------------");
                    Console.ResetColor();
                }
            }
        }
        // =====================================================================
        // HÀM HELPER: ĐỌC FILE WORD (.DOCX) BẰNG C# NATIVE (KHÔNG CẦN THƯ VIỆN BÊN THỨ 3)
        // =====================================================================
        public static string ExtractTextFromDocx(string filePath)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                using (ZipArchive zip = ZipFile.OpenRead(filePath))
                {
                    ZipArchiveEntry entry = zip.GetEntry("word/document.xml");
                    if (entry == null) return "";

                    using (Stream stream = entry.Open())
                    {
                        XmlDocument xmlDoc = new XmlDocument();
                        xmlDoc.Load(stream);

                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                        nsmgr.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");

                        XmlNode body = xmlDoc.SelectSingleNode("//w:body", nsmgr);
                        if (body != null)
                        {
                            foreach (XmlNode node in body.ChildNodes)
                            {
                                if (node.Name == "w:p") // Đọc đoạn văn bình thường
                                {
                                    string text = node.InnerText;
                                    if (!string.IsNullOrWhiteSpace(text))
                                        sb.AppendLine(text);
                                }
                                else if (node.Name == "w:tbl") // Đọc Bảng biểu (Cực kỳ quan trọng cho I/O List)
                                {
                                    sb.AppendLine("\n[START TABLE]");
                                    foreach (XmlNode row in node.SelectNodes(".//w:tr", nsmgr))
                                    {
                                        List<string> cellTexts = new List<string>();
                                        foreach (XmlNode cell in row.SelectNodes(".//w:tc", nsmgr))
                                        {
                                            cellTexts.Add(cell.InnerText.Trim());
                                        }
                                        sb.AppendLine(string.Join(" | ", cellTexts));
                                    }
                                    sb.AppendLine("[END TABLE]\n");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[ERROR] Cannot read Word file: {ex.Message}";
            }

            return sb.ToString();
        }

    }
}