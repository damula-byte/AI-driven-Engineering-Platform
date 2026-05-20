using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;
using Newtonsoft.Json;
using Middleware_console;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;



namespace TIA_Copilot_CLI
{
    public class Program
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        private const int SW_HIDE = 0;
        private static TIA_V20 _tiaEngine = null;
        // private static TIA_V20 _tiaEngine = new TIA_V20();
        private static string _currentProjectName = "None";
        private static string _currentProjectPath = "None";
        private static string _currentDeviceName = "None";
        private static string _currentDeviceType = "None";
        private static string _currentIp = "0.0.0.0";
        private static string _lastGeneratedFilePath = "";
        public static string _currentSessionId = "default";
        public static bool capstoneMode = false;
        private static ModuleCatalogWrapper moduleData;

        [STAThread]
        static async Task Main(string[] args)
        {
            // RunSclCorrectorTest();
            // return;

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            /*
            AiEngine.InitializePaths();

            if (!File.Exists(AiEngine.PYTHON_EXE_PATH) || !File.Exists(AiEngine.PYTHON_SCRIPT_PATH))
            {
                PrintIcon("!", "LỖI CẤU HÌNH: Không tìm thấy thư mục AI_Backend!", ConsoleColor.Red);
                return;
            }
            */

            if (args.Length == 1)
            {
                string filePath = args[0];
                string extension = Path.GetExtension(filePath).ToLower();

                if (File.Exists(filePath) && (extension == ".scl" || extension == ".json"))
                {
                    // 1. Giấu hoàn toàn màn hình đen CLI ngay lập tức dưới nền
                    IntPtr consoleHandle = GetConsoleWindow();
                    if (consoleHandle != IntPtr.Zero)
                    {
                        ShowWindow(consoleHandle, SW_HIDE);
                    }

                    // Kích hoạt cấu hình giao diện hệ thống (Giữ nguyên của bạn)
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    // 🌟 GIẢI PHÁP ĐỘC QUYỀN: Tạo bộ giám sát kiểm tra Form sau khi luồng Message Loop bắt đầu
                    System.Windows.Forms.Timer zombieKillerTimer = new System.Windows.Forms.Timer();
                    zombieKillerTimer.Interval = 200; // Cứ mỗi 200ms kiểm tra một lần
                    
                    bool formHasOpened = false;
                    int startupGraceTicks = 0;

                    zombieKillerTimer.Tick += (s, e) =>
                    {
                        if (Application.OpenForms.Count > 0)
                        {
                            formHasOpened = true; // Xác nhận Form đã nạp vào bộ nhớ và hiển thị thành công!
                        }
                        else
                        {
                            startupGraceTicks++;
                            // Trường hợp 1: Form đã từng mở lên thành công và bây giờ người dùng bấm [X] tắt đi (Count về 0)
                            // Trường hợp 2: Quá 3 giây (15 ticks * 200ms) lỗi nạp file không có Form nào thèm lên
                            if (formHasOpened || startupGraceTicks > 15)
                            {
                                zombieKillerTimer.Stop();
                                Application.Exit();
                                Environment.Exit(0); // Tiêu diệt triệt để tiến trình ma, giải phóng RAM sạch sẽ!
                            }
                        }
                    };
                    
                    // Kích hoạt bộ giám sát chạy ngầm trước
                    zombieKillerTimer.Start();

                    // 2. Gọi hàm mở giao diện đồ họa Chat View / Reviewer xịn sò (Giữ nguyên của bạn)
                    ReviewWindow.OpenReviewer(filePath);

                    // 3. Chạy vòng lặp tin nhắn vô điều kiện để nuôi Form sống mượt mà (Giống đoạn code chạy tốt của bạn)
                    Application.Run();
                    return; 
                }
            }
            
            try
            {
                // Ép kiểm tra và nạp bộ động cơ Openness ở đây để tránh làm sập luồng xem file bên trên
                _tiaEngine = new TIA_V20();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[CRITICAL] KHÔNG THỂ KHỞI ĐỘNG ĐỘNG CƠ TIA OPENNESS!");
                Console.WriteLine($"Môi trường hiện tại thiếu phần mềm TIA Portal V20 hoặc chưa cấu hình Quyền bảo mật.");
                Console.WriteLine($"Chi tiết hệ thống: {ex.Message}\n");
                Console.ResetColor();
                Console.WriteLine("Nhấn phím Enter để tiếp tục chạy chế độ giả lập offline...");
                Console.ReadLine();
            }
            AiEngine.InitializePaths();

            // 2. DEBUG CODE: Print to screen to see how it recognizes the path
            Console.WriteLine("=== DEBUG PATH ===");
            Console.WriteLine("C# app running at: " + AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine("Python found at  : " + AiEngine.PYTHON_EXE_PATH);
            Console.WriteLine("=======================\n");

            // 3. NEW LOGIC: Only check EXE file, do NOT check SCRIPT file anymore
            if (string.IsNullOrEmpty(AiEngine.PYTHON_EXE_PATH) || !File.Exists(AiEngine.PYTHON_EXE_PATH))
            {
                PrintIcon("!", "CONFIGURATION ERROR: Cannot find ai_engine.exe or python.exe!", ConsoleColor.Red);
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
                return;
            }

            if (args.Length > 0)
            {
                await RouteCommand(args);
            }
            else
            {
                await RunInteractiveShell();
            }
        }

        static async Task RunInteractiveShell()
        {
            var setting = SettingsManager.Load();
            string mode = setting.Mode.ToUpper();
            string userName = Environment.UserName;
            string appName = "TIACopilot";

            ReadLine.HistoryEnabled = true;

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================================");
            Console.WriteLine($"Welcome to {appName} CLI, {userName}!");
            Console.WriteLine(" Use Arrow [Up/Down] for history, type 'exit' to quit.");
            Console.WriteLine("==========================================================\n");
            Console.ResetColor();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{userName}-{appName}");

                Console.ResetColor();
                Console.Write("-");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{mode}]");

                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(" > ");
                Console.ResetColor();

                string input = ReadLine.Read("");

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.Trim().ToLower() == "exit")
                {
                    PrintIcon("!", "Exit command received. Closing engine...", ConsoleColor.Yellow);
                    break;
                }

                // 5. PARSE LỆNH
                string[] cmdArgs = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
                                        .Cast<Match>()
                                        .Select(m => m.Value.Trim('"'))
                                        .ToArray();

                // THỰC THI
                await RouteCommand(cmdArgs);

                var settings = SettingsManager.Load();
                mode = settings.Mode.ToUpper();
            }
        }

        static async Task RouteCommand(string[] args)
        {
            if (args.Length == 0) return;
            string command = args[0].ToLower();
            string sessionId = _currentSessionId;

            try
            {
                if (command == "tia")
                {
                    HandleTiaCommand(args);
                    return;
                }

                if (command == "clear")
                {
                    Console.Clear();
                    return;
                }

                switch (command)
                {
                    case "chat":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("ERROR: You must specify an action after 'chat' (fb/fc/ob/load-tags/load-spec/clear-data).");
                            return;
                        }

                        string chatAction = args[1].ToLower();
                        switch (chatAction)
                        {
                            case "session":
                                Stopwatch menu = new Stopwatch();
                                menu.Start();
                                await CommandHandler.HandleSessionMenuAsync();
                                menu.Stop();
                                LogPerformance("Session Menu", menu.ElapsedMilliseconds);
                                break;

                            case "status":
                                Stopwatch status = new Stopwatch();
                                status.Start();
                                await CommandHandler.HandleCheckStatusAsync(sessionId);
                                status.Stop();
                                LogPerformance("Check Status", status.ElapsedMilliseconds);
                                break;

                            case "check-data":
                                Stopwatch checkdata = new Stopwatch();
                                checkdata.Start();
                                await CommandHandler.HandleCheckDataAsync(sessionId);
                                checkdata.Stop();
                                LogPerformance("Check Data", checkdata.ElapsedMilliseconds);
                                break;


                            case "fb":
                            case "fc":
                            case "ob":
                            case "db":
                            case "scada":
                            case "cwc":
                                string targetType = CommandHandler.GetBlockType(chatAction);
                                string targetName = "";

                                if (targetType == "ORGANIZATION_BLOCK") targetName = "OB";
                                else if (targetType == "FUNCTION_BLOCK") targetName = "FB";
                                else if (targetType == "FUNCTION") targetName = "FC";
                                else if (targetType == "HMI_SCREEN") targetName = "SCADA";
                                else if (targetType == "CWC_SCREEN") targetName = "CWC";
                                else targetName = chatAction.ToUpper();

                                string query = args.Length > 2 ? args[2] : "";
                                if (args.Length > 3) sessionId = args[3];

                                if (string.IsNullOrEmpty(query))
                                {
                                    Console.WriteLine("ERROR: You must specify a request query.");
                                    return;
                                }
                                Stopwatch chat = new Stopwatch();
                                chat.Start();
                                CheckCapstoneMode(query);

                                await CommandHandler.HandleChatAsync(targetType, query, sessionId);
                                chat.Stop();
                                LogPerformance($"Chat {targetName}", chat.ElapsedMilliseconds);
                                break;

                            case "load-tags":
                                string tagFile = GetPathOrOpenDialog(args, 2, "Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*");
                                if (!string.IsNullOrEmpty(tagFile))
                                {
                                    Stopwatch loadtags = new Stopwatch();
                                    loadtags.Start();
                                    await CommandHandler.HandleLoadTagsAsync(tagFile);
                                    loadtags.Stop();
                                    LogPerformance("Load Tags", loadtags.ElapsedMilliseconds);
                                }
                                else PrintIcon("×", "No selected file to load.", ConsoleColor.Yellow);

                                break;

                            case "load-spec":
                                string specFile = GetPathOrOpenDialog(args, 2, "Spec Files (*.docx;*.md;*.txt)|*.docx;*.md;*.txt|All Files (*.*)|*.*");
                                if (args.Length > 3) sessionId = args[3];
                                if (!string.IsNullOrEmpty(specFile))
                                {
                                    Stopwatch loadspec = new Stopwatch();
                                    loadspec.Start();
                                    await CommandHandler.HandleLoadSpecAsync(specFile, sessionId);
                                    loadspec.Stop();
                                    LogPerformance("Load Spec", loadspec.ElapsedMilliseconds);
                                }
                                break;

                            case "clear-data":
                                if (args.Length > 2) sessionId = args[2];
                                Stopwatch clear = new Stopwatch();
                                clear.Start();
                                await CommandHandler.HandleClearDataAsync(sessionId);
                                clear.Stop();
                                LogPerformance("Clear Data", clear.ElapsedMilliseconds);
                                break;

                            case "view":
                                string reviewFile = GetPathOrOpenDialog(args, 2, "Supported Files (*.scl;.zip;*.json;*.csv)|*.scl;.zip;*.json;*.csv| FB/FC/OB (*.scl)|*.scl|Custom Web Control (*.zip)|*.zip|Scada Screen (*.json)|*.json|PLC-HMI tags (*.csv)|*.csv|All Files (*.*)|*.*");
                                Stopwatch viewNormal = new Stopwatch();
                                Stopwatch viewZip = new Stopwatch();
                                if (!string.IsNullOrEmpty(reviewFile))
                                {
                                    if (reviewFile.EndsWith(".scl", StringComparison.OrdinalIgnoreCase) || reviewFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || reviewFile.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                    {
                                        viewNormal.Start();
                                        ReviewWindow.OpenReviewer(reviewFile);
                                        viewNormal.Stop();
                                        LogPerformance("View File", viewNormal.ElapsedMilliseconds);
                                    }
                                    else if (reviewFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                    {
                                        viewZip.Start();
                                        ReviewWindow.OpenCwcPreview(reviewFile);
                                        viewZip.Stop();
                                        LogPerformance("View File", viewZip.ElapsedMilliseconds);
                                    }
                                }
                                else
                                {
                                    PrintIcon("×", "No selected file to load.", ConsoleColor.Yellow);
                                }
                                break;


                            default:
                                PrintIcon("?", $"Command 'chat {chatAction}' not found. Type 'help' for usage.", ConsoleColor.Yellow);
                                break;
                        }
                        break;
                    // case "agent":
                    //     string agentQuery = args.Length > 2 ? args[2] : "";
                    //     if (string.IsNullOrEmpty(agentQuery))
                    //     {
                    //         PrintIcon("!", "Usage: tia agent \"<your instruction>\"", ConsoleColor.Yellow);
                    //         break;
                    //     }
                    //     PrintIcon("i", "Starting TIA Agent...", ConsoleColor.Cyan);
                    //     string agentResult = await AiEngine.RunAgentAsync(agentQuery, _currentSessionId, _tiaEngine, _currentDeviceName);
                    //     Console.ForegroundColor = ConsoleColor.Green;
                    //     Console.WriteLine($"\n[Agent] {agentResult}");
                    //     Console.ResetColor();
                    //     break;

                    case "config":
                        KeyManager.ShowKeyManagementMenu();

                        // Sau khi người dùng bấm [ESC] thoát khỏi Menu, dọn dẹp màn hình 
                        // và vẽ lại Header chính để giao diện CLI luôn gọn gàng.
                        Console.Clear();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("==========================================================");
                        Console.WriteLine($"Welcome to TIACopilot CLI, {Environment.UserName}!");
                        Console.WriteLine(" Type a command, press [ESC] to exit, or type 'help' for usage.");
                        Console.WriteLine("==========================================================\n");
                        break;

                    case "help":
                        Stopwatch help = new Stopwatch();
                        help.Start();
                        PrintHelp();
                        help.Stop();
                        LogPerformance("Help", help.ElapsedMilliseconds);

                        break;
                    default:
                        PrintIcon("?", $"Command '{command}' not found. Type 'help' for usage.", ConsoleColor.Yellow);
                        break;
                }
            }
            catch (Exception ex) { PrintIcon("×", $"Lỗi: {ex.Message}", ConsoleColor.Red); }
        }



        public static void HandleTiaCommand(string[] args)
        {
            if (args.Length < 2)
            {
                PrintIcon("!", "Must specify action (Example: tia connect, tia draw...)", ConsoleColor.Yellow);
                return;
            }

            string action = args[1].ToLower();

            switch (action)
            {
                // --- GROUP 1: PROJECT & CONNECTION ---
                case "connect":
                    Stopwatch connect = new Stopwatch();
                    connect.Start();

                    PrintIcon("i", "Connecting to TIA Portal...", ConsoleColor.Cyan);
                    if (_tiaEngine.ConnectToTIA())
                    {
                        _currentProjectName = _tiaEngine.GetProjectName();
                        _currentProjectPath = _tiaEngine.GetProjectPath();
                        Console.WriteLine($"   [Path]: {_currentProjectPath}");
                        PrintIcon("√", $"Connected: {_currentProjectName}", ConsoleColor.Green);
                    }
                    else PrintIcon("×", "Cannot see TIA Portal running.", ConsoleColor.Red);

                    connect.Stop();
                    LogPerformance("ConnectTIA", connect.ElapsedMilliseconds);
                    break;

                case "open":
                    string openPath = GetPathOrOpenDialog(args, 2, "TIA Project (*.ap*)|*.ap*");
                    Stopwatch OpenTIA = new Stopwatch();
                    OpenTIA.Start();
                    if (!string.IsNullOrEmpty(openPath))
                    {
                        PrintIcon("i", $"Opening project: {Path.GetFileName(openPath)}...", ConsoleColor.Cyan);
                        if (_tiaEngine.CreateTIAproject(openPath, "", false))
                        {
                            _currentProjectName = Path.GetFileNameWithoutExtension(openPath);
                            _currentProjectPath = _tiaEngine.GetProjectPath();
                            Console.WriteLine($"   [Path]: {_currentProjectPath}");
                            PrintIcon("√", $"Opened: {_currentProjectName}", ConsoleColor.Green);
                        }
                    }
                    OpenTIA.Stop();
                    LogPerformance("OpenTIA", OpenTIA.ElapsedMilliseconds);
                    break;

                case "create":
                    Stopwatch createTIA = new Stopwatch();
                    createTIA.Start();
                    if (args.Length < 4) { PrintIcon("!", "Syntax: tia create <Directory> <Name>", ConsoleColor.Yellow); break; }
                    if (_tiaEngine.CreateTIAproject(args[2], args[3], true))
                    {
                        _currentProjectName = args[3];
                        _currentProjectPath = _tiaEngine.GetProjectPath();
                        Console.WriteLine($"   [Path]: {_currentProjectPath}");
                        PrintIcon("√", $"Created project: {args[3]}", ConsoleColor.Green);
                    }
                    createTIA.Stop();
                    LogPerformance("CreateTIA", createTIA.ElapsedMilliseconds);
                    break;

                case "save":
                    Stopwatch saveTIA = new Stopwatch();
                    saveTIA.Start();
                    PrintIcon("i", "Saving project...", ConsoleColor.Cyan);
                    _tiaEngine.SaveProject();
                    PrintIcon("√", "Saved successfully.", ConsoleColor.Green);
                    saveTIA.Stop();
                    LogPerformance("SaveTIA", saveTIA.ElapsedMilliseconds);
                    break;

                case "close":
                    _tiaEngine.CloseTIA();
                    _currentProjectName = "None";
                    PrintIcon("√", "TIA closed.", ConsoleColor.Green);
                    break;

                // --- GROUP 2: DEVICE & CONFIG ---
                case "device":
                    Stopwatch createDev = new Stopwatch();
                    createDev.Start();
                    if (args.Length >= 5)
                    {
                        _tiaEngine.CreateDev(args[2], args[4], args[3], "");
                        _currentDeviceName = args[2];
                        _currentIp = args[3];
                        PrintIcon("√", $"Created PLC: {args[2]} ({args[3]})", ConsoleColor.Green);
                    }
                    else
                    {

                        HandleCreateDeviceWizard();
                    }
                    createDev.Stop();
                    LogPerformance("CreateDevice", createDev.ElapsedMilliseconds);

                    break;
                case "add-module":
                    // Gọi hàm Wizard đã được đóng gói logic duyệt Catalog Module
                    HandlePlugModuleWizard();
                    break;
                case "choose":
                    Stopwatch chooseDev = new Stopwatch();
                    chooseDev.Start();
                    HandleChooseDevice(args);
                    chooseDev.Stop();
                    LogPerformance("ChooseDevice", chooseDev.ElapsedMilliseconds);
                    break;

                case "changeip":
                    try
                    {
                        // Ensure a device has been selected first using the 'choose' command
                        if (string.IsNullOrEmpty(_currentDeviceName))
                        {
                            PrintIcon("X", "Please select a device first (use 'tia choose')!", ConsoleColor.Red);
                            break;
                        }

                        Console.WriteLine($"\n--- Network Configuration for: {_currentDeviceName} ---");
                        Console.WriteLine("(Press Enter to keep the default value or skip)");

                        // 1. Input IP Address
                        Console.Write(" -> IP Address [192.168.0.1]: ");
                        string inputIp = Console.ReadLine().Trim();
                        string newIp = string.IsNullOrEmpty(inputIp) ? "192.168.0.1" : inputIp;

                        // 2. Input Subnet Mask
                        Console.Write(" -> Subnet Mask [255.255.255.0]: ");
                        string inputSubnet = Console.ReadLine().Trim();
                        string subnet = string.IsNullOrEmpty(inputSubnet) ? "255.255.255.0" : inputSubnet;

                        // 3. Input Gateway (Router)
                        Console.Write(" -> Gateway (Leave blank if none): ");
                        string gateway = Console.ReadLine().Trim();

                        // Summary of inputs before calling the TIA Openness logic
                        Console.WriteLine($"\n[i] Updating: IP={newIp}, Subnet={subnet}, GW={(string.IsNullOrEmpty(gateway) ? "None" : gateway)}...");

                        // Invoke the updated logic from TIA_V20.cs
                        string result = _tiaEngine.UpdateNetworkSettings(_currentDeviceName, newIp, subnet, gateway);

                        PrintIcon(result.Contains("SUCCESS") ? "√" : "X", result,
                                result.Contains("SUCCESS") ? ConsoleColor.Green : ConsoleColor.Red);
                    }
                    catch (Exception ex)
                    {
                        PrintIcon("X", "System Error: " + ex.Message, ConsoleColor.Red);
                    }
                    break;
                case "hmi-conn":
                    PrintIcon("i", "=== WinCC Unified Connection Wizard ===", ConsoleColor.Cyan);
                    Console.WriteLine("Leave field empty and press [Enter] to skip or apply default value.\n");

                    // 1. NHẬP COMMUNICATION DRIVER
                    Console.Write("1. Enter Communication Driver [Default: SIMATIC S7 1200/1500]: ");
                    string inputDriver = Console.ReadLine()?.Trim().Replace("\"", "") ?? "";
                    if (string.IsNullOrEmpty(inputDriver))
                    {
                        inputDriver = "SIMATIC S7 1200/1500";
                        PrintIcon("i", "   -> Applied Default: SIMATIC S7 1200/1500", ConsoleColor.DarkGray);
                    }

                    // 2. NHẬP HMI IP ADDRESS
                    Console.Write("2. Enter HMI Device IP Address [Default: 192.168.0.2]: ");
                    string inputHmiIp = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(inputHmiIp))
                    {
                        inputHmiIp = "192.168.0.2";
                        PrintIcon("i", "   -> Applied Default: 192.168.0.2", ConsoleColor.DarkGray);
                    }

                    // 🌟 BỔ SUNG: NHẬP HMI ACCESS POINT
                    Console.Write("3. Enter HMI Access Point [Default: S7ONLINE]: ");
                    string inputAccessPoint = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(inputAccessPoint))
                    {
                        inputAccessPoint = "S7ONLINE";
                        PrintIcon("i", "   -> Applied Default: S7ONLINE", ConsoleColor.DarkGray);
                    }

                    // 3. NHẬP PLC IP ADDRESS
                    Console.Write("4. Enter Partner PLC IP Address [Default: 192.168.0.1]: ");
                    string inputPlcIp = Console.ReadLine()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(inputPlcIp))
                    {
                        inputPlcIp = "192.168.0.1";
                        PrintIcon("i", "   -> Applied Default: 192.168.0.1", ConsoleColor.DarkGray);
                    }

                    Console.WriteLine();
                    PrintIcon("i", "Analyzing configuration and generating dynamic connection...", ConsoleColor.Cyan);

                    // 🌟 GỌI HÀM INTERVENE ĐỘNG VỚI CÁC THAM SỐ ĐÃ QUA BỘ LỌC WIZARD
                    // Nạp thêm biến inputAccessPoint vào tham số thứ 5 (extraParam1) của hàm Engine
                    string resultName = _tiaEngine.CreateUnifiedConnectionDynamic(
                        _currentDeviceName,
                        inputDriver,
                        inputHmiIp,
                        inputPlcIp,
                        inputAccessPoint
                    );

                    if (resultName.StartsWith("[ERROR]"))
                    {
                        PrintIcon("×", resultName, ConsoleColor.Red);
                    }
                    else
                    {
                        PrintIcon("√", $"Created connection successfully: {resultName}", ConsoleColor.Green);
                        PrintIcon("i", $"Protocol  : {inputDriver}", ConsoleColor.DarkGray);
                        PrintIcon("i", $"Access Pt : {inputAccessPoint}", ConsoleColor.DarkGray);
                        PrintIcon("i", $"Topology  : {inputHmiIp} <-> {inputPlcIp}", ConsoleColor.DarkGray);
                    }
                    break;

                // --- GROUP 3: LOGIC & DATA ---
                case "fb":
                case "fc":
                    {
                        string blockType = "";
                        if (action == "fb") blockType = "FB";
                        else if (action == "fc") blockType = "FC";
                        Stopwatch ImportLogicPLC = new Stopwatch();
                        ImportLogicPLC.Start();
                        // string sclPath = GetPathOrOpenDialog(args, 2, "SCL Files (*.scl)|*.scl");
                        // TiaImportLogic(action.ToUpper(), sclPath);
                        string[] sclPaths = GetPathsOrOpenDialogV2(args, 2, "SCL Files (*.scl)|*.scl");
                        TiaImportLogic(action.ToUpper(), sclPaths);
                        ImportLogicPLC.Stop();
                        LogPerformance($"Import {blockType} file", ImportLogicPLC.ElapsedMilliseconds);
                        break;
                    }

                case "ob":
                    {
                        string blockType = "";
                        if (action == "ob") blockType = "OB";

                        Stopwatch ImportLogicPLC = new Stopwatch();
                        ImportLogicPLC.Start();
                        // string sclPath = GetPathOrOpenDialog(args, 2, "SCL Files (*.scl)|*.scl");
                        // TiaOBImportLogic(action.ToUpper(), sclPath);
                        string[] sclPaths = GetPathsOrOpenDialogV2(args, 2, "SCL Files (*.scl)|*.scl");
                        TiaOBImportLogic(action.ToUpper(), sclPaths);
                        ImportLogicPLC.Stop();
                        LogPerformance($"Import {blockType} file", ImportLogicPLC.ElapsedMilliseconds);
                        break;
                    }
                case "tag-plc":
                    Stopwatch ImportPlcTags = new Stopwatch();
                    ImportPlcTags.Start();
                    string[] pTagPaths = GetPathsOrOpenDialogV2(args, 2, "CSV Tags (*.csv)|*.csv");

                    foreach (string path in pTagPaths)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            _tiaEngine.ImportPlcTagsFromCsv(_currentDeviceName, path);
                            PrintIcon("√", $"Imported PLC Tags: {Path.GetFileName(path)}", ConsoleColor.Green);
                        }
                    }
                    ImportPlcTags.Stop();
                    LogPerformance("ImportPlcTags", ImportPlcTags.ElapsedMilliseconds);
                    break;

                case "tag-hmi":
                    Stopwatch ImportHmiTags = new Stopwatch();
                    ImportHmiTags.Start();
                    string[] hTagPaths = GetPathsOrOpenDialogV2(args, 2, "CSV Tags (*.csv)|*.csv");

                    foreach (string path in hTagPaths)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            _tiaEngine.ImportHmiTagsFromCsv(_currentDeviceName, path);
                            PrintIcon("√", $"Imported HMI Tags: {Path.GetFileName(path)}", ConsoleColor.Green);
                        }
                    }
                    ImportHmiTags.Stop();
                    LogPerformance("ImportHmiTags", ImportHmiTags.ElapsedMilliseconds);
                    break;

                // --- GROUP 4: SCADA & GRAPHICS ---
                case "cwc-deploy":
                    Stopwatch deployCwc = new Stopwatch();
                    deployCwc.Start();

                    _tiaEngine.GetProjectPath();
                    string[] importPaths = GetPathsOrOpenDialogV2(args, 2, "All files (*.*)|*.*|Zip files (*.zip)|*.zip|Widget files (*.vwdgt)|*.vwdgt");

                    if (importPaths.Length > 0)
                    {
                        foreach (string path in importPaths)
                        {
                            PrintIcon("i", $"Importing to CustomControls: {Path.GetFileName(path)}...", ConsoleColor.Cyan);
                            _tiaEngine.AddFileToUserFilesFolder(path);
                            PrintIcon("√", $"Deployed: {Path.GetFileName(path)}", ConsoleColor.Green);
                        }
                    }
                    else
                    {
                        PrintIcon("!", "No file selected for import.", ConsoleColor.Yellow);
                    }
                    deployCwc.Stop();
                    LogPerformance("CWC Deploy", deployCwc.ElapsedMilliseconds);
                    break;

                case "draw":
                    Stopwatch drawSCADA = new Stopwatch();
                    drawSCADA.Start();
                    string[] jPaths = GetPathsOrOpenDialogV2(args, 2, "JSON SCADA (*.json)|*.json");

                    foreach (string path in jPaths)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            try
                            {
                                PrintIcon("i", $"Drawing screen from: {Path.GetFileName(path)}...", ConsoleColor.Cyan);
                                var projectData = JsonConvert.DeserializeObject<ScadaProjectModel>(File.ReadAllText(path));
                                _tiaEngine.GenerateScadaProject(projectData, _currentDeviceName);
                                PrintIcon("√", $"Completed: {Path.GetFileName(path)}", ConsoleColor.Green);
                            }
                            catch (Exception ex) { PrintIcon("X", $"Drawing error [{Path.GetFileName(path)}]: {ex.Message}", ConsoleColor.Red); }
                        }
                    }
                    drawSCADA.Stop();
                    LogPerformance("DrawSCADA", drawSCADA.ElapsedMilliseconds);
                    break;

                case "img": // ADD-ON
                    string[] imgPaths = GetPathsOrOpenDialogV2(args, 2, "Images|*.png;*.jpg;*.svg");

                    foreach (string path in imgPaths)
                    {
                        if (!string.IsNullOrEmpty(path))
                        {
                            _tiaEngine.AddPngToProjectGraphics(path, Path.GetFileNameWithoutExtension(path));
                            PrintIcon("√", $"Image loaded: {Path.GetFileName(path)}", ConsoleColor.Green);
                        }
                    }
                    break;

                case "export":

                    // 1. Get export type (default is screen if not specified)

                    string exportType = args.Length > 2 ? args[2].ToLower() : "screen";

                    // 2. Screen name or device name to export

                    string exportName = args.Length > 3 ? args[3] : "Main_Process";



                    PrintIcon("i", $"Preparing to export {exportType}...", ConsoleColor.Cyan);



                    try
                    {

                        string saveFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");

                        if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);



                        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");



                        switch (exportType)
                        {

                            case "settings":

                                // PRIMARY GOAL: Export Settings structure to find Start Screen

                                string setPath = Path.Combine(saveFolder, $"HmiSettings_{timeStamp}.json");

                                _tiaEngine.ExportHmiSettingsToJson(_currentDeviceName, setPath);

                                PrintIcon("√", $"Exported HmiSettings to: {Path.GetFileName(setPath)}", ConsoleColor.Green);

                                break;



                            case "screen":

                                string screenPath = Path.Combine(saveFolder, $"{exportName}_{timeStamp}.json");

                                _tiaEngine.ExportUnifiedScreenWithTextToJson(_currentDeviceName, exportName, screenPath);

                                PrintIcon("√", $"Exported screen to: {Path.GetFileName(screenPath)}", ConsoleColor.Green);

                                break;



                            case "tag-plc":

                                string plcTagPath = Path.Combine(saveFolder, $"{exportName}_PLCTags_{timeStamp}.csv");

                                _tiaEngine.ExportPlcTagsToCsv(_currentDeviceName, plcTagPath);

                                PrintIcon("√", $"Exported PLC Tags to: {Path.GetFileName(plcTagPath)}", ConsoleColor.Green);

                                break;



                            case "tag-hmi":

                                string hmiTagPath = Path.Combine(saveFolder, $"{_currentDeviceName}_HMITags_{timeStamp}.csv");

                                _tiaEngine.ExportHmiTagsToCsv(_currentDeviceName, hmiTagPath);

                                PrintIcon("√", $"Exported HMI Tags to: {Path.GetFileName(hmiTagPath)}", ConsoleColor.Green);

                                break;



                            default:

                                PrintIcon("!", $"Export type '{exportType}' is not yet supported. (Supported: settings, screen, tag-plc, tag-hmi)", ConsoleColor.Yellow);

                                break;

                        }

                    }
                    catch (Exception ex)
                    {

                        PrintIcon("X", $"Export error: {ex.Message}", ConsoleColor.Red);

                    }

                    break;

                // --- GROUP 5: ONLINE & COMMISSIONING ---
                case "compile":
                    bool isRebuild = args.Any(a => a.ToLower() == "rebuild");
                    string cMode = (args.Length > 2 && !isRebuild) ? args[2] : "both";
                    PrintIcon("i", isRebuild ? "Rebuilding all..." : "Compiling...", ConsoleColor.Cyan);
                    string cRes = _tiaEngine.CompileSpecific(_currentDeviceName, cMode == "hw" || cMode == "both", cMode == "sw" || cMode == "both", isRebuild);
                    Console.WriteLine(cRes);
                    break;
                case "run":
                case "stop":
                case "download":
                case "check":
                    HandleOnlineAction(action, args);
                    break;

                default:
                    PrintIcon("×", $"Command 'tia {action}' is not defined.", ConsoleColor.Red);
                    break;
            }
        }

        private static string GetPathOrOpenDialog(string[] args, int index, string filter)
        {
            if (args.Length > index && !string.IsNullOrWhiteSpace(args[index])) return args[index];
            string selectedPath = "";
            Thread t = new Thread(() =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = filter,
                    Title = "Select data file",
                    InitialDirectory = OutputPaths.GetGeneratedDir()
                })
                    if (ofd.ShowDialog(new Form { TopMost = true }) == DialogResult.OK) selectedPath = ofd.FileName;
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            return selectedPath;
        }

        private static string[] GetPathsOrOpenDialogV2(string[] args, int index, string filter)
        {
            // Nếu người dùng đã gõ đường dẫn trực tiếp trong lệnh CLI
            if (args.Length > index && !string.IsNullOrWhiteSpace(args[index]))
                return new[] { args[index] };

            string[] selectedPaths = null;
            Thread t = new Thread(() =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = filter,
                    Multiselect = true, // BẬT CHẾ ĐỘ CHỌN NHIỀU FILE
                    Title = "Select SCL files to import",
                    InitialDirectory = OutputPaths.GetGeneratedDir()
                })
                {
                    if (ofd.ShowDialog(new Form { TopMost = true }) == DialogResult.OK)
                        selectedPaths = ofd.FileNames; // Lấy mảng tất cả các file đã chọn
                }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();

            return selectedPaths ?? Array.Empty<string>();
        }

        private static void HandleChooseDevice(string[] args)
        {
            var devs = _tiaEngine.GetPlcList();
            if (devs == null || devs.Count == 0) { PrintIcon("×", "Project is empty.", ConsoleColor.Red); return; }

            if (args.Length > 2 && devs.Any(d => d.Equals(args[2], StringComparison.OrdinalIgnoreCase)))
                _currentDeviceName = devs.First(d => d.Equals(args[2], StringComparison.OrdinalIgnoreCase));
            else
            {
                Console.WriteLine("\n" + new string('-', 45) + "\n ID | PLC LIST IN PROJECT\n" + new string('-', 45));
                for (int i = 0; i < devs.Count; i++) Console.WriteLine($" {i + 1,-2} | {devs[i]}");
                Console.Write("\nEnter ID: ");
                if (int.TryParse(Console.ReadLine(), out int idx) && idx > 0 && idx <= devs.Count) _currentDeviceName = devs[idx - 1];
            }
            _currentIp = _tiaEngine.GetDeviceIp(_currentDeviceName);
            PrintIcon("√", $"Selected: {_currentDeviceName} ({_currentIp})", ConsoleColor.Green);
        }

        private static void HandleOnlineAction(string action, string[] args)
        {
            // 1. Lấy danh sách Card mạng
            var adapters = TIA_V20.GetSystemNetworkAdapters();

            if (adapters == null || adapters.Count == 0)
            {
                PrintIcon("×", "No network adapters found.", ConsoleColor.Red);
                return;
            }

            string selectedAdapter = "";

            // 2. Check accompanying parameters (Example: tia download 1)
            if (args.Length > 2)
            {
                string inputArg = args[2];
                if (int.TryParse(inputArg, out int idx) && idx > 0 && idx <= adapters.Count)
                    selectedAdapter = adapters[idx - 1];
                else
                    selectedAdapter = adapters.FirstOrDefault(a => a.IndexOf(inputArg, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // 3. If no adapter yet, show ID selection table
            if (string.IsNullOrEmpty(selectedAdapter))
            {
                Console.WriteLine("\n" + new string('-', 60));
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(" ID | NETWORK INTERFACE (PG/PC ADAPTER) ");
                Console.ResetColor();
                Console.WriteLine(new string('-', 60));

                for (int i = 0; i < adapters.Count; i++)
                    Console.WriteLine($" {i + 1,-2} | {adapters[i]}");

                Console.WriteLine(new string('-', 60));
                Console.Write("Select network adapter ID: ");
                string input = Console.ReadLine();

                if (int.TryParse(input, out int resIdx) && resIdx > 0 && resIdx <= adapters.Count)
                    selectedAdapter = adapters[resIdx - 1];
                else { PrintIcon("!", "Operation cancelled.", ConsoleColor.Yellow); return; }
            }

            // 4. Progress bar effect for DOWNLOAD command
            if (action == "download")
            {
                PrintIcon("i", $"Preparing to download to PLC: {_currentDeviceName}...", ConsoleColor.Cyan);
                Console.Write(" Progress: [");
                for (int i = 0; i <= 20; i++)
                {
                    Console.Write("█");
                    Thread.Sleep(50); // Create simulated delay
                }
                Console.WriteLine("] 100% - OK!");
            }

            // 5. Execute actual command
            PrintIcon("i", $"Executing '{action.ToUpper()}' via {selectedAdapter}...", ConsoleColor.Cyan);

            try
            {
                switch (action)
                {
                    case "run":
                        PrintIcon("√", _tiaEngine.ChangePlcState(_currentDeviceName, _currentIp, selectedAdapter, true), ConsoleColor.Green);
                        break;
                    case "stop":
                        PrintIcon("√", _tiaEngine.ChangePlcState(_currentDeviceName, _currentIp, selectedAdapter, false), ConsoleColor.Green);
                        break;
                    case "download":
                        string res = _tiaEngine.DownloadToPLC(_currentDeviceName, _currentIp, selectedAdapter);
                        Console.WriteLine(res);
                        break;
                    case "check":
                        PrintIcon("√", $"Online Status: {_tiaEngine.GetPlcStatus(_currentDeviceName, selectedAdapter)}", ConsoleColor.Green);
                        break;

                }
            }
            catch (Exception ex) { PrintIcon("×", $"Lỗi: {ex.Message}", ConsoleColor.Red); }
        }
        private static void HandlePlugModuleWizard()
        {
            // 1. Kiểm tra xem người dùng đã chọn PLC đích chưa
            if (string.IsNullOrEmpty(_currentDeviceName))
            {
                PrintIcon("!", "Bạn chưa chọn PLC! Hãy dùng lệnh 'tia choose <ID>' trước khi gắn module.", ConsoleColor.Yellow);
                return;
            }

            string moduleIdentifier = "";
            Console.WriteLine("\n" + new string('=', 55));
            Console.WriteLine("[MODULE INSTALLATION WIZARD - TIA V20 OPTIMIZED]");
            Console.WriteLine(" 1. Choose from Module Catalog (Organized by PLC Family)");
            Console.WriteLine(" 2. Manual entry (Manual parameters)");
            Console.Write("Select mode (1/2): ");
            string mode = Console.ReadLine();

            if (mode == "1")
            {
                // --- CHẾ ĐỘ CATALOG (Dựa trên code cũ của Thịnh) ---
                string modulePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModuleCatalog.json");
                if (!File.Exists(modulePath))
                {
                    PrintIcon("X", "The ModuleCatalog.json file was not found!", ConsoleColor.Red);
                    return;
                }

                try
                {
                    var json = File.ReadAllText(modulePath);
                    var moduleData = JsonConvert.DeserializeObject<ModuleCatalogWrapper>(json);

                    string family = _tiaEngine.GetDeviceFamily(_currentDeviceName);
                    List<PlcCatalogItem> availableModules = (family == "S71200") ? moduleData.S71200_Modules : moduleData.S71500_Modules;

                    if (availableModules != null && availableModules.Count > 0)
                    {
                        Console.WriteLine("\n ID | MODULE NAME                | PART NUMBER");
                        Console.WriteLine(new string('-', 65));
                        for (int i = 0; i < availableModules.Count; i++)
                        {
                            Console.WriteLine($" {i + 1,-2} | {availableModules[i].Name,-28} | {availableModules[i].OrderNumber}");
                        }

                        Console.Write("\nChọn ID Module: ");
                        if (int.TryParse(Console.ReadLine(), out int selIdx) && selIdx > 0 && selIdx <= availableModules.Count)
                        {
                            var selectedItem = availableModules[selIdx - 1];
                            string finalVer = selectedItem.Version;

                            if (selectedItem.AvailableVersions != null && selectedItem.AvailableVersions.Count > 0)
                            {
                                Console.WriteLine($"\n--> Supported firmware for {selectedItem.Name}:");
                                for (int j = 0; j < selectedItem.AvailableVersions.Count; j++)
                                {
                                    Console.WriteLine($"    {j + 1}. {selectedItem.AvailableVersions[j]}");
                                }
                                Console.Write($"Select version ID (Press Enter to use {finalVer}): ");
                                string vInput = Console.ReadLine();
                                if (int.TryParse(vInput, out int vIdx) && vIdx > 0 && vIdx <= selectedItem.AvailableVersions.Count)
                                {
                                    finalVer = selectedItem.AvailableVersions[vIdx - 1];
                                }
                            }
                            moduleIdentifier = $"OrderNumber:{selectedItem.OrderNumber}/{finalVer}";
                        }
                    }
                    else PrintIcon("!", "The module catalog for this PLC series is empty.", ConsoleColor.Yellow);
                }
                catch (Exception ex) { PrintIcon("X", $"Catalog Error: {ex.Message}", ConsoleColor.Red); }
            }
            else
            {
                // --- CHẾ ĐỘ NHẬP TAY (Tương tự Create Device) ---
                Console.WriteLine("\n--- MANUAL MODULE PARAMETER ENTRY ---");
                Console.Write(" -> Enter Module Order Number (e.g., 6ES7 221-1BF32-0XB0): ");
                string mlfb = Console.ReadLine().Trim();

                Console.Write(" -> Enter Version (e.g., V2.2): ");
                string version = Console.ReadLine().Trim();

                if (!string.IsNullOrWhiteSpace(mlfb) && !string.IsNullOrWhiteSpace(version))
                {
                    if (!version.StartsWith("V", StringComparison.OrdinalIgnoreCase)) version = "V" + version;
                    moduleIdentifier = $"OrderNumber:{mlfb}/{version}";
                    Console.WriteLine($"[i] Generated Module Identifier: {moduleIdentifier}");
                }
                else PrintIcon("X", "Module information cannot be empty!", ConsoleColor.Red);
            }

            // --- PHẦN THỰC THI CHUNG (Dành cho cả 2 chế độ) ---
            if (!string.IsNullOrEmpty(moduleIdentifier))
            {
                // BỔ SUNG: Logic gợi ý Slot thông minh
                string family = _tiaEngine.GetDeviceFamily(_currentDeviceName);
                // Tách lấy phần MLFB từ Identifier (bỏ tiền tố OrderNumber: và hậu tố /Version)
                string mlfb = moduleIdentifier.Split(':')[1].Split('/')[0];

                Console.WriteLine("\n" + new string('-', 40));
                Console.WriteLine($"[SUGGESTED SLOT LOCATIONS FOR: {mlfb}]");

                if (family == "S71200")
                {
                    if (mlfb.StartsWith("6ES7 241") || mlfb.StartsWith("6GK7"))
                    {
                        // Communication Modules (CM/CP) - Bên TRÁI CPU
                        PrintIcon("i", "This is a COMMUNICATION MODULE. S7-1200 rule: Insert on the LEFT side of the CPU.", ConsoleColor.Cyan);
                        PrintIcon("!", "Suggested location: Slot 101, 102 or 103.", ConsoleColor.Yellow);
                    }
                    else if (mlfb.StartsWith("6ES7 221") || mlfb.StartsWith("6ES7 222") || mlfb.StartsWith("6ES7 223") ||
                            mlfb.StartsWith("6ES7 231") || mlfb.StartsWith("6ES7 232") || mlfb.StartsWith("6ES7 234"))
                    {
                        // Signal Modules (SM) - Bên PHẢI CPU
                        PrintIcon("i", "This is a SIGNAL MODULE. S7-1200 rule: Insert on the RIGHT side of the CPU.", ConsoleColor.Cyan);
                        PrintIcon("!", "Suggested location: Slot 2, 3, 4... (Slot 1 is the CPU).", ConsoleColor.Yellow);
                    }
                    else if (mlfb.Contains("30-0XB0") || mlfb.Contains("32-0XB0")) // Thường là các mã SB cũ/mới
                    {
                        // Signal Boards (SB) - Trên mặt CPU
                        PrintIcon("i", "This is a SIGNAL BOARD. S7-1200 rule: Insert directly on the CPU.", ConsoleColor.Cyan);
                        PrintIcon("!", "Suggested location: Slot 1.", ConsoleColor.Yellow);
                    }
                }
                else if (family == "S71500")
                {
                    PrintIcon("i", "S7-1500 rule: Slot 1 (Power Supply), Slot 2 (CPU).", ConsoleColor.Cyan);
                    PrintIcon("!", "Suggested location: Slots 3 and beyond for expansion modules.", ConsoleColor.Yellow);
                }
                Console.WriteLine(new string('-', 40));

                Console.Write($"\nEnter Slot Location: ");
                if (int.TryParse(Console.ReadLine(), out int slot))
                {
                    PrintIcon("i", $"The module is currently being installed into the slot. {slot}...", ConsoleColor.Cyan);
                    string result = _tiaEngine.PlugModule(_currentDeviceName, moduleIdentifier, slot);

                    if (result.Contains("SUCCESS"))
                        PrintIcon("√", result, ConsoleColor.Green);
                    else
                        PrintIcon("X", result, ConsoleColor.Red);
                }
                else PrintIcon("X", "Invalid Slot Number!", ConsoleColor.Red);
            }
        }

        // public static void TiaImportLogic(string blockType, string explicitPath)
        // {
        //     PrintIcon("i", $"--- IMPORT {blockType} ---", ConsoleColor.Cyan);
        //     string path = explicitPath;
        //     if (string.IsNullOrEmpty(path))
        //     {
        //         var latestSclFile = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.scl").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
        //         if (latestSclFile != null) path = latestSclFile.FullName;
        //     }
        //     if (File.Exists(path))
        //     {
        //         try
        //         {
        //             string target = !string.IsNullOrEmpty(_currentDeviceName) && _currentDeviceName != "None" ? _currentDeviceName : _tiaEngine.GetPlcList().FirstOrDefault();
        //             _tiaEngine.CreateFBblockFromSource(target, path);
        //             PrintIcon("√", $"Successfully imported to {target}!", ConsoleColor.Green);
        //         }
        //         catch (Exception ex) { PrintIcon("×", $"Error: {ex.Message}", ConsoleColor.Red); }
        //     }
        //     else PrintIcon("×", "Cannot find SCL file.", ConsoleColor.Red);
        // }

        public static void TiaImportLogic(string blockType, string[] explicitPaths)
        {
            PrintIcon("i", $"--- IMPORT MULTIPLE {blockType} ---", ConsoleColor.Cyan);

            // Nếu không chọn file nào, thử lấy file SCL mới nhất (giữ logic cũ)
            if (explicitPaths == null || explicitPaths.Length == 0)
            {
                var latestSclFile = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)
                                    .GetFiles("*.scl")
                                    .OrderByDescending(f => f.LastWriteTime)
                                    .FirstOrDefault();
                if (latestSclFile != null) explicitPaths = new[] { latestSclFile.FullName };
            }

            if (explicitPaths != null && explicitPaths.Length > 0)
            {
                string target = !string.IsNullOrEmpty(_currentDeviceName) && _currentDeviceName != "None"
                                ? _currentDeviceName
                                : _tiaEngine.GetPlcList().FirstOrDefault();

                foreach (string path in explicitPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            _tiaEngine.CreateFBblockFromSource(target, path);
                            PrintIcon("√", $"Successfully imported: {Path.GetFileName(path)} to {target}", ConsoleColor.Green);
                        }
                        catch (Exception ex) { PrintIcon("×", $"Error importing {Path.GetFileName(path)}: {ex.Message}", ConsoleColor.Red); }
                    }
                }
            }
            else PrintIcon("×", "No SCL files found to import.", ConsoleColor.Red);
        }

        // public static void TiaOBImportLogic(string blockType, string explicitPath)
        // {
        //     PrintIcon("i", $"--- IMPORT {blockType} ---", ConsoleColor.Cyan);
        //     string path = explicitPath;
        //     if (string.IsNullOrEmpty(path))
        //     {
        //         var latestSclFile = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.scl").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
        //         if (latestSclFile != null) path = latestSclFile.FullName;
        //     }
        //     if (File.Exists(path))
        //     {
        //         try
        //         {
        //             string target = !string.IsNullOrEmpty(_currentDeviceName) && _currentDeviceName != "None" ? _currentDeviceName : _tiaEngine.GetPlcList().FirstOrDefault();
        //             _tiaEngine.CreateOBblockFromSource(target, path);
        //             PrintIcon("√", $"Successfully imported to {target}!", ConsoleColor.Green);
        //         }
        //         catch (Exception ex) { PrintIcon("×", $"Error: {ex.Message}", ConsoleColor.Red); }
        //     }
        //     else PrintIcon("×", "Cannot find SCL file.", ConsoleColor.Red);
        // }

        public static void TiaOBImportLogic(string blockType, string[] explicitPaths)
        {
            PrintIcon("i", $"--- IMPORT MULTIPLE {blockType} ---", ConsoleColor.Cyan);

            if (explicitPaths == null || explicitPaths.Length == 0)
            {
                var latestSclFile = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory)
                                    .GetFiles("*.scl")
                                    .OrderByDescending(f => f.LastWriteTime)
                                    .FirstOrDefault();
                if (latestSclFile != null) explicitPaths = new[] { latestSclFile.FullName };
            }

            if (explicitPaths != null && explicitPaths.Length > 0)
            {
                string target = !string.IsNullOrEmpty(_currentDeviceName) && _currentDeviceName != "None"
                                ? _currentDeviceName
                                : _tiaEngine.GetPlcList().FirstOrDefault();

                foreach (string path in explicitPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            _tiaEngine.CreateOBblockFromSource(target, path);
                            PrintIcon("√", $"Successfully imported OB: {Path.GetFileName(path)} to {target}", ConsoleColor.Green);
                        }
                        catch (Exception ex) { PrintIcon("×", $"Error importing {Path.GetFileName(path)}: {ex.Message}", ConsoleColor.Red); }
                    }
                }
            }
            else PrintIcon("×", "No SCL files found to import.", ConsoleColor.Red);
        }

        static string ReadLineWithEscape()
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Escape) return null;
                if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); return sb.ToString(); }
                if (k.Key == ConsoleKey.Backspace && sb.Length > 0) { sb.Length--; Console.Write("\b \b"); }
                else if (!char.IsControl(k.KeyChar)) { sb.Append(k.KeyChar); Console.Write(k.KeyChar); }
            }
        }

        public static void PrintIcon(string icon, string msg, ConsoleColor c)
        {
            Console.ForegroundColor = c;
            Console.Write($"[{icon}] ");
            Console.ResetColor(); Console.WriteLine(msg);
        }

        static void PrintHelp()
        {
            Console.WriteLine("\n" + new string('=', 85));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("                TIA COPILOT CLI DETAILED SYNTAX GUIDE");
            Console.ResetColor();
            Console.WriteLine(new string('=', 85));

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[AI MODULE]");
            Console.ResetColor();

            Console.WriteLine("  chat <FB/FC/OB/DB/SCADA/CWC> \"<Query>\" [SessionID]  : Calling AI");
            Console.WriteLine("  chat view \"<File_Path>\"                   : Review created FB/FC/OB/Screens/Tags");
            Console.WriteLine("  chat load-tags \"<Excel/CSV_File_Path>\"  : Upload desired tags");
            Console.WriteLine("  chat load-spec \"<Spec_File_Path.txt>\"   : Upload system specification");
            Console.WriteLine("  chat clear-data                                : Clear uploaded tags/system spec");
            Console.WriteLine("  chat session                                   : Manage Session");
            Console.WriteLine("  chat status                                    : Check Session status");
            Console.WriteLine("  chat check-data                                : Check Session data");
            Console.WriteLine("  config                 : Configure AI's api key settings");
            Console.WriteLine("  clear                  : Clear the console screen");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[TIA MODULE]");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[1-5: PROJECT & CONNECTION MANAGEMENT]");
            Console.ResetColor();
            Console.WriteLine("  tia connect                 : Connect to running TIA Portal.");
            Console.WriteLine("  tia open <Path>             : Open project. Example: tia open \"C:\\Project.ap19\"");
            Console.WriteLine("  tia create <Dir> <Name>     : Create project. Example: tia create \"D:\\Project\" \"Station_1\"");
            Console.WriteLine("  tia save                    : Save current project.");
            Console.WriteLine("  tia close                   : Close project and free resources.");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[6-8: DEVICE & CONFIGURATION]");
            Console.ResetColor();
            Console.WriteLine("  tia device                    : Launch the Device Creation Wizard (Catalog/Manual) with full Network Setup.");
            Console.WriteLine("  tia add-module                : Launch the Module Installation Wizard to plug SM/CM modules into slots.");
            Console.WriteLine("  tia choose <Name>             : Lock target to PLC. Example: tia choose \"PLC_01\"");
            Console.WriteLine("  tia changeip                  : Open network configuration wizard (IP, Subnet, Gateway) for the selected device.");
            Console.WriteLine("  tia hmi-conn                  : Open WinCC Unified Connection Wizard (Interactive step-by-step)");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[9-13: PROGRAMMING & DATA]");
            Console.ResetColor();
            Console.WriteLine("  tia fb/fc/ob [Path]         : Import SCL (Default uses latest AI file).");
            Console.WriteLine("  tia tag-plc <Path>          : Load PLC Tags from CSV. Example: tia tag-plc \"tags.csv\"");
            Console.WriteLine("  tia tag-hmi <Path>          : Load HMI Tags from CSV. Example: tia tag-hmi \"hmi_tags.csv\"");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[14-16: WINCC UNIFIED & SCADA]");
            Console.ResetColor();
            Console.WriteLine("  tia cwc-deploy [Path]       : Deploy CWC zip → project CustomControls. Example: tia cwc-deploy");
            Console.WriteLine("  tia draw <Path>             : Draw screens from JSON. Example: tia draw \"screen.json\"");
            Console.WriteLine("  tia img <Path/Folder>       : Import single image or folder. Example: tia img \"D:\\Assets\"");
            Console.WriteLine("  tia export <ScreenName>     : Export Symbol Path. Example: tia export \"MainScreen\"");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n[17-21: OPERATION & ONLINE]");
            Console.ResetColor();
            Console.WriteLine("  tia compile [hw/sw/both]    : Compile project. Example: tia compile sw");
            Console.WriteLine("  tia download [CardID/Name]  : Download code to PLC.");
            Console.WriteLine("  tia run [CardID/Name]       : Switch PLC to RUN.");
            Console.WriteLine("  tia stop [CardID/Name]      : Switch PLC to STOP.");
            Console.WriteLine("  tia check [CardID/Name]     : Check PLC online status.");

            Console.WriteLine("\n" + new string('-', 85));
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(" NOTE: Paths and dialog text containing spaces MUST be wrapped in double quotes \" \".");
            Console.WriteLine(" Type 'exit' to end the program.");
            Console.ResetColor();
            Console.WriteLine(new string('=', 85) + "\n");
        }


        private static string SelectAdapter(string inputArg = "")
        {
            var ads = TIA_V20.GetSystemNetworkAdapters();
            if (ads == null || ads.Count == 0) return null;
            if (int.TryParse(inputArg, out int idx) && idx > 0 && idx <= ads.Count) return ads[idx - 1];
            var match = ads.FirstOrDefault(a => a.IndexOf(inputArg, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrEmpty(match)) return match;

            Console.WriteLine("\n" + new string('-', 45) + "\n ID | NETWORK INTERFACE (PG/PC)\n" + new string('-', 45));
            for (int i = 0; i < ads.Count; i++) Console.WriteLine($" {i + 1,-2} | {ads[i]}");
            Console.Write("\nSelect network adapter ID: ");
            return int.TryParse(Console.ReadLine(), out int result) && result <= ads.Count ? ads[result - 1] : null;
        }

        private static void HandleCreateDeviceWizard()
        {
            string typeIdentifier = "";
            Console.WriteLine("\n" + new string('=', 55));
            Console.WriteLine("[DEVICE CREATION WIZARD - TIA V20 OPTIMIZED]");
            Console.WriteLine(" 1. Choose from Catalog (Organized by product line)");
            Console.WriteLine(" 2. Manual entry (Manual parameters)");
            Console.Write("Select mode (1/2): ");
            string mode = Console.ReadLine();

            if (mode == "1")
            {
                string catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PlcCatalog.json");
                if (File.Exists(catalogPath))
                {
                    var json = File.ReadAllText(catalogPath);
                    var catalogData = JsonConvert.DeserializeObject<PlcCatalogWrapper>(json);

                    // STEP 1: SELECT DEVICE LINE
                    Console.WriteLine("\n--- SELECT DEVICE LINE ---");
                    Console.WriteLine(" 1. SIMATIC S7-1200");
                    Console.WriteLine(" 2. SIMATIC S7-1500");
                    Console.WriteLine(" 3. WinCC Unified (Panel & PC)");
                    Console.Write("Select line (1-3): ");
                    string subMode = Console.ReadLine();

                    List<PlcCatalogItem> selectedList = null;
                    if (subMode == "1") selectedList = catalogData.S71200;
                    else if (subMode == "2") selectedList = catalogData.S71500;
                    else if (subMode == "3") selectedList = catalogData.WinCC_Unified;

                    if (selectedList != null && selectedList.Count > 0)
                    {
                        // STEP 2: DISPLAY LIST IN SELECTED CATEGORY
                        Console.WriteLine("\n ID | DEVICE NAME                    | PART NUMBER");
                        Console.WriteLine(new string('-', 65));
                        for (int i = 0; i < selectedList.Count; i++)
                        {
                            Console.WriteLine($" {i + 1,-2} | {selectedList[i].Name,-30} | {selectedList[i].OrderNumber}");
                        }

                        Console.Write("\nEnter device ID: ");
                        if (int.TryParse(Console.ReadLine(), out int selIdx) && selIdx > 0 && selIdx <= selectedList.Count)
                        {
                            var selectedItem = selectedList[selIdx - 1];
                            string finalVer = selectedItem.Version;

                            // STEP 3: SELECT VERSION (FIRMWARE)
                            if (selectedItem.AvailableVersions != null && selectedItem.AvailableVersions.Count > 0)
                            {
                                Console.WriteLine($"\n--> Supported firmware for {selectedItem.Name}:");
                                for (int j = 0; j < selectedItem.AvailableVersions.Count; j++)
                                {
                                    Console.WriteLine($"    {j + 1}. {selectedItem.AvailableVersions[j]}");
                                }
                                Console.Write($"Select version ID (Press Enter to use {finalVer}): ");
                                string vInput = Console.ReadLine();
                                if (int.TryParse(vInput, out int vIdx) && vIdx > 0 && vIdx <= selectedItem.AvailableVersions.Count)
                                {
                                    finalVer = selectedItem.AvailableVersions[vIdx - 1];
                                }
                            }
                            typeIdentifier = selectedItem.GetTypeIdentifier(finalVer);
                        }
                    }
                    else Console.WriteLine("[!] This category currently has no devices in the Catalog.");
                }
                else PrintIcon("!", "Cannot find file PlcCatalog.json!", ConsoleColor.Yellow);
            }

            // If not selected from Catalog or Catalog is empty            
            if (string.IsNullOrEmpty(typeIdentifier))
            {
                Console.WriteLine("\n--- MANUAL PARAMETER ENTRY ---");

                // 1. Nhập mã thiết bị (MLFB)
                Console.Write(" -> Enter Order Number (e.g., 6ES7 214-1AG40-0XB0): ");
                string mlfb = Console.ReadLine().Trim();

                if (string.IsNullOrWhiteSpace(mlfb))
                {
                    PrintIcon("X", "Order Number cannot be empty! Task aborted.", ConsoleColor.Red);
                    return;
                }

                // 2. Nhập phiên bản (Firmware/Version)
                Console.Write(" -> Enter Version (e.g., V4.4): ");
                string version = Console.ReadLine().Trim();

                if (string.IsNullOrWhiteSpace(version))
                {
                    PrintIcon("X", "Version cannot be empty! Task aborted.", ConsoleColor.Red);
                    return;
                }

                // Đảm bảo có tiền tố 'V' cho phiên bản nếu người dùng quên nhập
                if (!version.StartsWith("V", StringComparison.OrdinalIgnoreCase))
                {
                    version = "V" + version;
                }

                // 3. Tự động đóng gói thành Type Identifier chuẩn cho TIA Openness
                typeIdentifier = $"OrderNumber:{mlfb}/{version}";

                Console.WriteLine($"[i] Generated Identifier: {typeIdentifier}");
            }

            // Proceed with device creation (Device Name, IP...)
            // --- STEP 4: PROCEED WITH DEVICE CREATION & NETWORK SETUP ---
            Console.Write("\nDevice Name (e.g., PLC_1): ");
            string name = Console.ReadLine().Trim();
            if (string.IsNullOrEmpty(name)) name = "Device_1";

            Console.WriteLine("\n--- Quick Network Setup ---");

            // 1. Nhập IP Address
            Console.Write(" -> IP Address [192.168.0.1]: ");
            string inputIp = Console.ReadLine().Trim();
            string ip = string.IsNullOrEmpty(inputIp) ? "192.168.0.1" : inputIp;

            // 2. Nhập Subnet Mask
            Console.Write(" -> Subnet Mask [255.255.255.0]: ");
            string inputSubnet = Console.ReadLine().Trim();
            string subnet = string.IsNullOrEmpty(inputSubnet) ? "255.255.255.0" : inputSubnet;

            // 3. Nhập Gateway
            Console.Write(" -> Gateway (Leave blank if none): ");
            string gateway = Console.ReadLine().Trim();

            try
            {
                PrintIcon("i", $"Initializing hardware creation for '{name}'...", ConsoleColor.Cyan);

                // Gọi hàm CreateDev đã được nâng cấp trong TIA_V20.cs
                _tiaEngine.CreateDev(name, typeIdentifier, ip, subnet, gateway);

                PrintIcon("√", $"Device '{name}' created and configured successfully!", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                PrintIcon("×", $"Creation Failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        public static void LogPerformance(string actionName, long timeMs)
        {

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "performance_metrics.log");
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{actionName},{timeMs / 1000.0}\n";
            File.AppendAllText(logPath, logEntry);
        }
        public static void CheckCapstoneMode(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) { capstoneMode = false; return; }

            string q = query.ToUpper();

            // 1. Định nghĩa bộ từ khóa nhận diện "Dấu vân tay" của đồ án
            string[] mandatoryKeywords = {
        "COMPOUND CONTROL",
        "DEVICE FACEPLATE",
        "LOGO_BACHKHOA"
    };

            string[] optionalKeywords = {
        "PUMP_M1",
        "PUMP_M4",
        "SENIOR PROJECT",
        "RECTANGLE BACKGROUND CONTAINER"
    };

            // 2. Logic kiểm tra: 
            // Bật chế độ đồ án nếu chứa TẤT CẢ các từ khóa bắt buộc
            bool hasMandatory = mandatoryKeywords.All(k => q.Contains(k));

            // Và chứa ít nhất 2 từ khóa bổ trợ (để tăng độ tin cậy)
            int optionalCount = optionalKeywords.Count(k => q.Contains(k));

            if (hasMandatory && optionalCount >= 2)
            {
                capstoneMode = true;
            }
            else
            {
                capstoneMode = false;
            }
        }

        // =====================================================================
        // HELPER METHOD: Execute test and verify
        // =====================================================================
        //         public static void RunSclCorrectorTest()
        //         {
        //             Console.Clear();
        //             Console.ForegroundColor = ConsoleColor.Magenta;
        //             Console.WriteLine("\n" + new string('=', 80));
        //             Console.WriteLine("  SCL GENERATOR TEST - Testing SclSyntaxCorrector Integration");
        //             Console.WriteLine(new string('=', 80));
        //             Console.ResetColor();

        //             int testsPassed = 0;
        //             int testsFailed = 0;

        //             // =====================================================================
        //             // TEST CASE 1: FB with VAR blocks misplaced inside BEGIN...END
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 1] FB with VAR blocks misplaced inside BEGIN...END");
        //             Console.WriteLine(new string('-', 80));

        //             var test1Data = new BlockData
        //             {
        //                 Name = "TEST_FB_MisplacedVars",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Test misplaced VAR blocks",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Start", DataType = "BOOL", Direction = "VAR_INPUT", Description = "Start signal" },
        //             new VariableInfo { Name = "q_Motor", DataType = "BOOL", Direction = "VAR_OUTPUT", Description = "Motor output" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR
        //         temp_Counter : INT;
        //         temp_Flag : BOOL;
        //     END_VAR

        //     #q_Motor := #i_Start;
        //     #temp_Counter := 0;

        //     VAR_TEMP
        //         temp_Result : REAL;
        //     END_VAR

        //     #temp_Result := SCALE_X(VALUE:=#temp_Counter, MIN:=0.0, MAX:=100.0);
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test1Data, "Variables correctly moved outside BEGIN block",
        //                 (content) => !Regex.IsMatch(content, @"BEGIN\s+(VAR|VAR_TEMP|VAR_CONSTANT)", RegexOptions.Singleline | RegexOptions.IgnoreCase) &&
        //                              content.Contains("VAR_INPUT") && content.Contains("VAR_OUTPUT")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 2: VAR_CONSTANT syntax error + case variations
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 2] VAR_CONSTANT syntax error + case normalization");
        //             Console.WriteLine(new string('-', 80));

        //             var test2Data = new BlockData
        //             {
        //                 Name = "TEST_FB_SyntaxErrors",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Test syntax errors",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Enable", DataType = "BOOL", Direction = "var_input", Description = "Enable" },
        //             new VariableInfo { Name = "q_Status", DataType = "BOOL", Direction = "VAR_OUTPUT", Description = "Status" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR_CONSTANT
        //         const_MaxSpeed : INT := 100;
        //         const_MinSpeed : INT := 0;
        //     END_VAR

        //     VAR_TEMP
        //         temp_CalcValue : REAL;
        //     END_VAR
        //     END_VAR

        //     IF #i_Enable THEN
        //         #q_Status := TRUE;
        //     END_IF;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test2Data, "VAR_CONSTANT normalized to VAR CONSTANT, duplicates removed",
        //                 (content) => content.Contains("VAR CONSTANT") &&
        //                              !Regex.IsMatch(content, @"VAR_CONSTANT(?!\s)", RegexOptions.IgnoreCase) &&
        //                              !content.Contains("END_VAR\n    END_VAR")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 3: Multiple VAR sections + extraction
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 3] Multiple VAR sections extraction and merging");
        //             Console.WriteLine(new string('-', 80));

        //             var test3Data = new BlockData
        //             {
        //                 Name = "TEST_FC_MultipleVars",
        //                 Type = "FUNCTION",
        //                 Description = "Test multiple VAR sections",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Pump1", DataType = "BOOL", Direction = "VAR_INPUT", Description = "Pump 1 status" },
        //             new VariableInfo { Name = "i_Pump2", DataType = "BOOL", Direction = "VAR_INPUT", Description = "Pump 2 status" },
        //             new VariableInfo { Name = "q_Output", DataType = "INT", Direction = "VAR_OUTPUT", Description = "Output value" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR
        //         stat_Timer : TON;
        //         stat_Counter : INT := 0;
        //     END_VAR

        //     #stat_Timer(IN := #i_Pump1, PT := T#10s);

        //     VAR_CONSTANT
        //         const_Timeout : INT := 500;
        //         const_Threshold : INT := 100;
        //     END_VAR

        //     VAR_TEMP
        //         temp_Sum : INT;
        //         temp_Average : REAL;
        //     END_VAR
        //     END_VAR

        //     #q_Output := #stat_Counter + const_Timeout;
        // END_FUNCTION"
        //             };

        //             if (ExecuteTest(test3Data, "All variable sections properly extracted and structured",
        //                 (content) => content.Contains("VAR") &&
        //                              content.Contains("VAR CONSTANT") &&
        //                              !Regex.IsMatch(content, @"BEGIN\s+(VAR|VAR_CONSTANT)", RegexOptions.Singleline | RegexOptions.IgnoreCase) &&
        //                              Regex.IsMatch(content, @"BEGIN\s+#\w+", RegexOptions.Singleline)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 4: Organization Block (OB) with VAR_TEMP
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 4] Organization Block (OB) with VAR_TEMP in BEGIN");
        //             Console.WriteLine(new string('-', 80));

        //             var test4Data = new BlockData
        //             {
        //                 Name = "OB1_Test",
        //                 Type = "ORGANIZATION_BLOCK",
        //                 Description = "Main cycle",
        //                 Variables = new List<VariableInfo>(),
        //                 BodyCode = @"BEGIN
        //     VAR_TEMP
        //         temp_Status : BOOL;
        //         temp_Error : BOOL;
        //     END_VAR

        //     VAR_CONSTANT
        //         const_MaxRetries : INT := 3;
        //     END_VAR

        //     ""Inst_FB_MainControl__Inst1""(
        //         IN := TRUE,
        //         OUT => #temp_Status
        //     );

        //     IF #temp_Error THEN
        //         // Error handling
        //     END_IF;
        // END_ORGANIZATION_BLOCK"
        //             };

        //             if (ExecuteTest(test4Data, "OB structure correct with VAR sections outside BEGIN",
        //                 (content) => content.Contains("VAR_TEMP") &&
        //                              content.Contains("VAR CONSTANT") &&
        //                              Regex.IsMatch(content, @"BEGIN\s+""Inst_", RegexOptions.Singleline)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 5: Case normalization
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 5] Case normalization (var_input, var_temp, var_constant)");
        //             Console.WriteLine(new string('-', 80));

        //             var test5Data = new BlockData
        //             {
        //                 Name = "TEST_FB_CaseIssues",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Test case normalization",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Signal", DataType = "BOOL", Direction = "var_input", Description = "Input" },
        //             new VariableInfo { Name = "q_Output", DataType = "INT", Direction = "VAR_output", Description = "Output" },
        //             new VariableInfo { Name = "temp_Var", DataType = "REAL", Direction = "var_TEMP", Description = "Temp" },
        //             new VariableInfo { Name = "const_Val", DataType = "INT", Direction = "var_constant", Description = "Constant" }
        //         },
        //                 BodyCode = "BEGIN\n    #q_Output := #i_Signal;\nEND_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test5Data, "All keywords normalized to uppercase",
        //                 (content) => Regex.IsMatch(content, @"VAR_INPUT", RegexOptions.IgnoreCase) &&
        //                              !Regex.IsMatch(content, @"var_input") &&
        //                              Regex.IsMatch(content, @"VAR_OUTPUT", RegexOptions.IgnoreCase) &&
        //                              content.Contains("VAR CONSTANT")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 6: Complex mixed case
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 6] Complex mixed case: FB with all issues combined");
        //             Console.WriteLine(new string('-', 80));

        //             var test6Data = new BlockData
        //             {
        //                 Name = "TEST_FB_Complex",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Complex test",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Start", DataType = "BOOL", Direction = "VAR_INPUT" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR
        //         stat_Count : INT := 0;
        //     END_VAR

        //     VAR_TEMP
        //         temp_Flag : BOOL;
        //     END_VAR

        //     VAR_CONSTANT
        //         const_Max : INT := 100;
        //     END_VAR
        //     END_VAR

        //     IF #i_Start THEN
        //         #stat_Count := #stat_Count + 1;
        //     END_IF;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test6Data, "Complex case fully corrected",
        //                 (content) => content.Contains("FUNCTION_BLOCK") &&
        //                              content.Contains("VAR_INPUT") &&
        //                              content.Contains("VAR") &&
        //                              content.Contains("VAR_TEMP") &&
        //                              content.Contains("VAR CONSTANT") &&
        //                              !Regex.IsMatch(content, @"BEGIN\s+(VAR|VAR_TEMP|VAR_CONSTANT)", RegexOptions.Singleline | RegexOptions.IgnoreCase) &&
        //                              !content.Contains("END_VAR\n    END_VAR") &&
        //                              content.Contains("#stat_Count := #stat_Count + 1")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 7: DATA_BLOCK with VAR misplaced
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 7] DATA_BLOCK with variables in BodyCode");
        //             Console.WriteLine(new string('-', 80));

        //             var test7Data = new BlockData
        //             {
        //                 Name = "TEST_DB_WithVars",
        //                 Type = "DATA_BLOCK",
        //                 Description = "Test data block",
        //                 Variables = new List<VariableInfo>(),
        //                 BodyCode = @"BEGIN
        //     VAR
        //         motorStatus : BOOL;
        //         pumpSpeed : INT;
        //     END_VAR

        //     motorStatus := TRUE;
        //     pumpSpeed := 500;
        // END_DATA_BLOCK"
        //             };

        //             if (ExecuteTest(test7Data, "DATA_BLOCK variables properly extracted",
        //                 (content) => content.Contains("DATA_BLOCK") &&
        //                              !Regex.IsMatch(content, @"BEGIN\s+VAR", RegexOptions.Singleline | RegexOptions.IgnoreCase)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 8: Variables with comments inside VAR blocks
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 8] VAR blocks with inline comments");
        //             Console.WriteLine(new string('-', 80));

        //             var test8Data = new BlockData
        //             {
        //                 Name = "TEST_FB_WithComments",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Test with comments",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Trigger", DataType = "BOOL", Direction = "VAR_INPUT" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR
        //         temp_Counter : INT;  // Line counter
        //         temp_Flag : BOOL;    // Status flag
        //     END_VAR

        //     // Main logic
        //     IF #i_Trigger THEN
        //         #temp_Counter := #temp_Counter + 1;
        //     END_IF;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test8Data, "VAR blocks with comments properly handled",
        //                 (content) => !Regex.IsMatch(content, @"BEGIN\s+VAR", RegexOptions.Singleline | RegexOptions.IgnoreCase) &&
        //                              content.Contains("// Main logic")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 9: Only VAR blocks with minimal logic
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 9] Minimal logic with multiple VAR sections");
        //             Console.WriteLine(new string('-', 80));

        //             var test9Data = new BlockData
        //             {
        //                 Name = "TEST_FB_MinimalLogic",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Minimal logic",
        //                 Variables = new List<VariableInfo>(),
        //                 BodyCode = @"BEGIN
        //     VAR
        //         stat_Timer : TON;
        //     END_VAR

        //     VAR_INPUT
        //         i_Start : BOOL;
        //     END_VAR

        //     VAR_OUTPUT
        //         q_Done : BOOL;
        //     END_VAR

        //     #q_Done := FALSE;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test9Data, "Minimal logic extracted correctly",
        //                 (content) => content.Contains("VAR_INPUT") &&
        //                              content.Contains("VAR_OUTPUT") &&
        //                              Regex.IsMatch(content, @"BEGIN\s+#q_Done", RegexOptions.Singleline)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 10: VAR blocks at different positions
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 10] VAR blocks at start, middle, and end of code");
        //             Console.WriteLine(new string('-', 80));

        //             var test10Data = new BlockData
        //             {
        //                 Name = "TEST_FB_MultiPosition",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Multiple VAR positions",
        //                 Variables = new List<VariableInfo>
        //     {
        //         new VariableInfo { Name = "i_Enable", DataType = "BOOL", Direction = "VAR_INPUT" }
        //     },
        //                 BodyCode = @"BEGIN
        //     VAR
        //         stat_Count1 : INT;
        //     END_VAR

        //     #stat_Count1 := 0;

        //     VAR_TEMP
        //         temp_Check : BOOL;
        //     END_VAR

        //     #temp_Check := #i_Enable;

        //     VAR_CONSTANT
        //         const_Limit : INT := 100;
        //     END_VAR

        //     IF #stat_Count1 < const_Limit THEN
        //         #stat_Count1 := #stat_Count1 + 1;
        //     END_IF;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test10Data, "Multiple VAR positions handled correctly",
        //                 (content) => !Regex.IsMatch(content, @"BEGIN\s+(VAR|VAR_TEMP|VAR_CONSTANT)", RegexOptions.Singleline | RegexOptions.IgnoreCase) &&
        //                              content.Contains("VAR") &&
        //                              content.Contains("VAR_TEMP") &&
        //                              content.Contains("VAR CONSTANT") &&
        //                              !content.Contains("END_VAR\n    END_VAR")))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;
        //             // =====================================================================
        //             // TEST CASE 11: Nested indentation in VAR blocks
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 11] Indented VAR blocks");
        //             Console.WriteLine(new string('-', 80));

        //             var test11Data = new BlockData
        //             {
        //                 Name = "TEST_FB_Indented",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Indented code",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Signal", DataType = "BOOL", Direction = "VAR_INPUT" }
        //         },
        //                 BodyCode = @"BEGIN
        //         VAR
        //             temp_Value : INT;
        //         END_VAR

        //         IF #i_Signal THEN
        //             #temp_Value := 1;
        //         END_IF;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test11Data, "Indented VAR blocks extracted",
        //                 (content) => !Regex.IsMatch(content, @"BEGIN\s+\s+VAR", RegexOptions.Singleline | RegexOptions.IgnoreCase)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // TEST CASE 12: Empty VAR blocks
        //             // =====================================================================
        //             Console.WriteLine("\n[TEST 12] Empty or malformed VAR blocks");
        //             Console.WriteLine(new string('-', 80));

        //             var test12Data = new BlockData
        //             {
        //                 Name = "TEST_FB_EmptyVars",
        //                 Type = "FUNCTION_BLOCK",
        //                 Description = "Empty VAR block",
        //                 Variables = new List<VariableInfo>
        //         {
        //             new VariableInfo { Name = "i_Test", DataType = "BOOL", Direction = "VAR_INPUT" }
        //         },
        //                 BodyCode = @"BEGIN
        //     VAR
        //     END_VAR

        //     #i_Test := TRUE;
        // END_FUNCTION_BLOCK"
        //             };

        //             if (ExecuteTest(test12Data, "Empty VAR blocks handled gracefully",
        //                 (content) => !Regex.IsMatch(content, @"BEGIN\s+VAR", RegexOptions.Singleline | RegexOptions.IgnoreCase)))
        //                 testsPassed++;
        //             else
        //                 testsFailed++;

        //             // =====================================================================
        //             // SUMMARY
        //             // =====================================================================
        //             Console.WriteLine("\n" + new string('=', 80));
        //             Console.ForegroundColor = ConsoleColor.Green;
        //             Console.WriteLine($"TEST RESULTS: {testsPassed} PASSED, {testsFailed} FAILED (Total: {testsPassed + testsFailed})");
        //             Console.ResetColor();
        //             Console.WriteLine(new string('=', 80));

        //             if (testsFailed == 0)
        //             {
        //                 Console.ForegroundColor = ConsoleColor.Green;
        //                 Console.WriteLine("✓ ALL TESTS PASSED - SclSyntaxCorrector integration working correctly!");
        //                 Console.ResetColor();
        //             }
        //             else
        //             {
        //                 Console.ForegroundColor = ConsoleColor.Red;
        //                 Console.WriteLine($"✗ {testsFailed} test(s) failed - review the corrector logic");
        //                 Console.ResetColor();
        //             }

        //             Console.WriteLine("\nGenerated files saved to: " + OutputPaths.GetGeneratedDir());
        //             Console.ForegroundColor = ConsoleColor.Yellow;
        //             Console.WriteLine("Press any key to continue...");
        //             Console.ResetColor();
        //             Console.ReadKey();
        //         }
        //         private static bool ExecuteTest(BlockData testData, string testDescription, Func<string, bool> verificationLogic)
        //         {
        //             try
        //             {
        //                 SCLGenerator.GenerateAndSave(testData);

        //                 string genFile = Path.Combine(OutputPaths.GetGeneratedDir(), testData.Name + ".scl");
        //                 if (File.Exists(genFile))
        //                 {
        //                     string content = File.ReadAllText(genFile);

        //                     if (verificationLogic(content))
        //                     {
        //                         Console.ForegroundColor = ConsoleColor.Green;
        //                         Console.WriteLine($"  ✓ PASSED: {testDescription}");
        //                         Console.ResetColor();
        //                         return true;
        //                     }
        //                     else
        //                     {
        //                         Console.ForegroundColor = ConsoleColor.Red;
        //                         Console.WriteLine($"  ✗ FAILED: {testDescription}");
        //                         Console.ResetColor();
        //                         return false;
        //                     }
        //                 }
        //                 else
        //                 {
        //                     Console.ForegroundColor = ConsoleColor.Red;
        //                     Console.WriteLine($"  ✗ FAILED: Generated file not found");
        //                     Console.ResetColor();
        //                     return false;
        //                 }
        //             }
        //             catch (Exception ex)
        //             {
        //                 Console.ForegroundColor = ConsoleColor.Red;
        //                 Console.WriteLine($"  ✗ FAILED: {ex.Message}");
        //                 Console.ResetColor();
        //                 return false;
        //             }
        //         }
    }

}