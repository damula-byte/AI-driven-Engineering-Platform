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


namespace TIA_Copilot_CLI
{
    public class Program
    {
        private static TIA_V20 _tiaEngine = new TIA_V20();
        private static string _currentProjectName = "None";
        private static string _currentProjectPath = "None";
        private static string _currentDeviceName = "None";
        private static string _currentDeviceType = "None";
        private static string _currentIp = "0.0.0.0";
        private static string _lastGeneratedFilePath = "";
        public static string _currentSessionId = "default";
        public static bool capstoneMode = false;

        [STAThread]
        static async Task Main(string[] args)
        {
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
             

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==========================================================");
            Console.WriteLine($"Welcome to {appName} CLI, {userName}!");
            Console.WriteLine(" Type a command, press [ESC] to exit, or type 'help' for usage.");
            Console.WriteLine("==========================================================\n");
            Console.ResetColor();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"{userName}-{appName}-[{mode}]");
                Console.ResetColor();
                Console.Write(" > ");

                string input = ReadLineWithEscape();

                if (input == null || input.Trim().ToLower() == "exit")
                {
                    PrintIcon("!", "Exit command received. Closing engine...", ConsoleColor.Yellow);
                    break;
                }

                if (string.IsNullOrWhiteSpace(input)) continue;

                string[] cmdArgs = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
                                        .Cast<Match>().Select(m => m.Value.Trim('"'))
                                        .ToArray();

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

                case "choose":
                    Stopwatch chooseDev = new Stopwatch();
                    chooseDev.Start();
                    HandleChooseDevice(args);
                    chooseDev.Stop();
                    LogPerformance("ChooseDevice", chooseDev.ElapsedMilliseconds);
                    break;

                case "hmi-conn":
                    if (args.Length < 4)
                    {
                        PrintIcon("!", "Syntax: tia hmi-conn <HMI_IP> <PLC_IP>", ConsoleColor.Yellow);
                        break;
                    }

                    PrintIcon("i", "Analyzing connection...", ConsoleColor.Cyan);

                    // Call function and receive the actual Connection name created (example: HMI_PLC_Conn_2)
                    string resultName = _tiaEngine.CreateUnifiedConnectionCombined(_currentDeviceName, args[2], args[3]);

                    if (resultName.StartsWith("[ERROR]"))
                    {
                        PrintIcon("×", resultName, ConsoleColor.Red);
                    }
                    else
                    {
                        PrintIcon("√", $"Created connection successfully: {resultName}", ConsoleColor.Green);
                        PrintIcon("i", $"Address: {args[2]} <-> {args[3]}", ConsoleColor.DarkGray);
                    }
                    break;

                // --- GROUP 3: LOGIC & DATA ---
                case "fb":
                case "fc":
                case "ob":
                    string blockType = "";
                    if (action == "fb") blockType = "FB";
                    else if (action == "fc") blockType = "FC";
                    else if (action == "ob") blockType = "OB";

                    Stopwatch ImportLogicPLC = new Stopwatch();
                    ImportLogicPLC.Start();
                    string sclPath = GetPathOrOpenDialog(args, 2, "SCL Files (*.scl)|*.scl");
                    TiaImportLogic(action.ToUpper(), sclPath);
                    ImportLogicPLC.Stop();
                    LogPerformance($"Import {blockType} file", ImportLogicPLC.ElapsedMilliseconds);
                    break;

                case "tag-plc":
                    Stopwatch ImportPlcTags = new Stopwatch();
                    ImportPlcTags.Start();
                    string pTagPath = GetPathOrOpenDialog(args, 2, "CSV Tags (*.csv)|*.csv");
                    if (!string.IsNullOrEmpty(pTagPath)) _tiaEngine.ImportPlcTagsFromCsv(_currentDeviceName, pTagPath);
                    ImportPlcTags.Stop();
                    LogPerformance("ImportPlcTags", ImportPlcTags.ElapsedMilliseconds);
                    break;

                case "tag-hmi":
                    Stopwatch ImportHmiTags = new Stopwatch();
                    ImportHmiTags.Start();
                    string hTagPath = GetPathOrOpenDialog(args, 2, "CSV Tags (*.csv)|*.csv");
                    if (!string.IsNullOrEmpty(hTagPath)) _tiaEngine.ImportHmiTagsFromCsv(_currentDeviceName, hTagPath);
                    ImportHmiTags.Stop();
                    LogPerformance("ImportHmiTags", ImportHmiTags.ElapsedMilliseconds);
                    break;

                // --- GROUP 4: SCADA & GRAPHICS ---
                case "cwc-deploy":
                    Stopwatch deployCwc = new Stopwatch();
                    deployCwc.Start();

                    _tiaEngine.GetProjectPath();
                    string importPath = GetPathOrOpenDialog(args, 2, "All files (*.*)|*.*|Zip files (*.zip)|*.zip|Widget files (*.vwdgt)|*.vwdgt");
                    if (!string.IsNullOrEmpty(importPath))
                    {
                        PrintIcon("i", $"Importing to CustomControls: {Path.GetFileName(importPath)}...", ConsoleColor.Cyan);

                        // 3. Perform physical copy to UserFiles/CustomControls
                        _tiaEngine.AddFileToUserFilesFolder(importPath);

                        PrintIcon("√", "Imported successfully.", ConsoleColor.Green);
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
                    string jPath = GetPathOrOpenDialog(args, 2, "JSON SCADA (*.json)|*.json");
                    if (!string.IsNullOrEmpty(jPath))
                    {
                        try
                        {
                            var projectData = JsonConvert.DeserializeObject<ScadaProjectModel>(File.ReadAllText(jPath));
                            _tiaEngine.GenerateScadaProject(projectData, _currentDeviceName);
                            PrintIcon("√", "SCADA drawing completed!", ConsoleColor.Green);
                        }
                        catch (Exception ex) { PrintIcon("X", $"Drawing error: {ex.Message}", ConsoleColor.Red); }
                    }
                    drawSCADA.Stop();
                    LogPerformance("DrawSCADA", drawSCADA.ElapsedMilliseconds);
                    break;

                case "img": // ADD-ON
                    string imgPath = GetPathOrOpenDialog(args, 2, "Images|*.png;*.jpg;*.svg");
                    if (!string.IsNullOrEmpty(imgPath))
                    {
                        _tiaEngine.AddPngToProjectGraphics(imgPath, Path.GetFileNameWithoutExtension(imgPath));
                        PrintIcon("√", "Image loaded to Graphics Folder.", ConsoleColor.Green);
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

        public static void TiaImportLogic(string blockType, string explicitPath)
        {
            PrintIcon("i", $"--- IMPORT {blockType} ---", ConsoleColor.Cyan);
            string path = explicitPath;
            if (string.IsNullOrEmpty(path))
            {
                var latestSclFile = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.scl").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (latestSclFile != null) path = latestSclFile.FullName;
            }
            if (File.Exists(path))
            {
                try
                {
                    string target = !string.IsNullOrEmpty(_currentDeviceName) && _currentDeviceName != "None" ? _currentDeviceName : _tiaEngine.GetPlcList().FirstOrDefault();
                    _tiaEngine.CreateFBblockFromSource(target, path);
                    PrintIcon("√", $"Successfully imported to {target}!", ConsoleColor.Green);
                }
                catch (Exception ex) { PrintIcon("×", $"Error: {ex.Message}", ConsoleColor.Red); }
            }
            else PrintIcon("×", "Cannot find SCL file.", ConsoleColor.Red);
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
            Console.WriteLine("  tia device <Name> <IP> <Type> : Create PLC. Example: tia device \"PLC_01\" \"192.168.0.1\" \"S7-1500\"");
            Console.WriteLine("  tia choose <Name>           : Lock target to PLC. Example: tia choose \"PLC_01\"");
            Console.WriteLine("  tia hmi-conn <H_IP> <P_IP>  : Connect HMI-PLC. Example: tia hmi-conn \"192.168.0.2\" \"192.168.0.1\"");

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
            Console.WriteLine(" Type 'exit' or press [ESC] to end the session.");
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
                // ... (Keep original Manual Input logic as before) ...
            }

            // Proceed with device creation (Device Name, IP...)
            Console.Write("\nDevice Name: "); string name = Console.ReadLine();
            Console.Write("IP Address: "); string ip = Console.ReadLine();

            try
            {
                PrintIcon("i", $"Creating {name}...", ConsoleColor.Cyan);
                _tiaEngine.CreateDev(name, typeIdentifier, ip, "");
                PrintIcon("√", $"Device '{name}' created successfully!", ConsoleColor.Green);
            }
            catch (Exception ex) { PrintIcon("×", $"Lỗi: {ex.Message}", ConsoleColor.Red); }
        }
        public static void LogPerformance(string actionName, long timeMs)
        {

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "performance_metrics.log");
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{actionName},{timeMs / 1000.0}\n";
            File.AppendAllText(logPath, logEntry);
        }
        // public static void CheckCapstoneMode(string query)
        // {
        // if (query == @"Generate a screen with 1 HmiGraphicView named it: Logo_BachKhoa; then create 4 horizontal faceplates for Pump_M1, Pump_M2, Pump_M3 and Pump_M4. Strictly follow the COMPOUND CONTROL — DEVICE FACEPLATE strategy: Start by drawing a Rectangle Background Container for each Pump. Group Start/Stop/Reset buttons/ 1 IOField output displaying the pump's running time , 1 HmiToggleSwitch and 2 Indicators of running , fault (these indicators are created using Circle) inside the gray frame as per the layout rules. Next, create 4 pumps (Pump_M1, Pump_M2, Pump_M3 where is create using ClassicPump, and Pump_M4, where Pump_M4 is created using a HorizontalPumpLeft type pump) ;  4 button (On_Valve_01, Off_Valve_01, On_Valve_02, Off_Valve_02); 2 button (Start_Auto, Stop_Auto); 2 tank (Tank_01, Tank_02); 7 PipeHorizontal (Pipe_1, Pipe_2, Pipe_3 , Pipe_4, Pipe_5, Pipe_6, Pipe_7); 2 ControlValve; 4 sensor using rectangle (Hi_1, Lo_1, Hi_2, Lo_2); 1 HmiText with content Senior Project (and named this HmiText is Header)")
        //                         {
        //                             capstoneMode = true;
        //                         }
        //                         else capstoneMode = false;
        // }
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
    }

}