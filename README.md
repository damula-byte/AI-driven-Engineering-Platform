<div align="center">

# 🏭 TIA Portal Copilot
### AI-Driven Engineering Platform for Industrial Automation

*AI-powered autonomous middleware for TIA Portal: Translating plain text into seamless project configuration, logic generation, and automated execution.*

<br/>

![Python](https://img.shields.io/badge/Python-3.11-3776AB?style=for-the-badge&logo=python&logoColor=white)
![CSharp](https://img.shields.io/badge/C%23-.NET_4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Gemini](https://img.shields.io/badge/Gemini_3.5_Flash-AI_Engine-4285F4?style=for-the-badge&logo=google&logoColor=white)
![ChromaDB](https://img.shields.io/badge/ChromaDB-Vector_Store-FF6B35?style=for-the-badge)
![SQLite](https://img.shields.io/badge/SQLite-Session_Memory-003B57?style=for-the-badge&logo=sqlite&logoColor=white)
![TIA Portal](https://img.shields.io/badge/TIA_Portal-V20-009999?style=for-the-badge&logo=siemens&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)

<br/>

</div>

---

## 📖 About The Project

Modern industrial automation engineering demands deep expertise across IEC 61131-3 programming, SCADA design, and hardware configuration — tasks that are time-intensive, error-prone, and require highly specialized knowledge. **TIA Portal Copilot** eliminates this bottleneck.

This platform operates as an intelligent middleware layer that accepts natural language instructions from the engineer and autonomously translates them into verified, production-ready artifacts — **SCL function blocks**, **WinCC Unified HMI screens**, **PLC tag tables**, and **Custom Web Controls (CWC)** — injecting them directly into a live Siemens TIA Portal project via the Openness API, without manual intervention.

The system is designed around three foundational principles:

- **Domain Fidelity** — A triple-path Retrieval-Augmented Generation (RAG) architecture grounds every generation in curated IEC 61131-3 and Siemens engineering heuristics, preventing hallucination of invalid syntax or non-existent instructions.
- **Strict Schema Enforcement** — All AI outputs are constrained to formal JSON schemas before downstream processing, ensuring the C# execution layer never receives malformed payloads.
- **Non-Destructive Injection** — The Openness API integration is designed to augment existing TIA Portal projects safely, with no modification to hardware configurations or existing program blocks unless explicitly instructed.

> **Disclaimer:** This is a research and productivity tool. All AI-generated code must be reviewed and validated by a qualified automation engineer before deployment to physical hardware. The authors accept no liability for damage arising from unreviewed code execution on industrial systems.

---

## 🏗️ System Architecture

The platform is composed of two decoupled core modules communicating over a bidirectional **stdin/stdout JSON pipe (IPC)**. This design enforces a clean boundary between AI reasoning and system execution, and ensures the Python AI engine can be upgraded or replaced without modifying the C# middleware.

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Engineer (CLI / UI)                          │
│              Natural language command + optional spec/tags          │
└──────────────────────────────┬──────────────────────────────────────┘
                               │  stdin  (JSON payload)
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│               MODULE 1 — AI Generative Engine  (Python 3.11)        │
│                                                                     │
│  ┌─────────────────┐   ┌──────────────────┐   ┌─────────────────┐  │
│  │  Triple-Path RAG │   │  Prompt Assembly  │   │  Gemini 3.5     │  │
│  │                 │   │                  │   │  Flash (LLM)    │  │
│  │ • iec_standard  │──▶│ • System rules   │──▶│                 │  │
│  │ • hmi_standard  │   │ • RAG context    │   │  JSON Output    │  │
│  │ • cwc_standard  │   │ • Spec context   │   │  (strict schema)│  │
│  │ • project_spec  │   │ • Chat history   │   └────────┬────────┘  │
│  └────────┬────────┘   └──────────────────┘            │           │
│           │  ChromaDB                                   │           │
│  ┌────────▼────────┐                                   │           │
│  │  Session Memory  │◀──────── SQLite (sliding window) │           │
│  └─────────────────┘                                   │           │
└────────────────────────────────────────────────────────┼───────────┘
                               │  stdout (JSON response) │
                               ▼
┌─────────────────────────────────────────────────────────────────────┐
│            MODULE 2 — Execution Middleware  (C# / .NET 4.8)         │
│                                                                     │
│  ┌─────────────────┐   ┌──────────────────┐   ┌─────────────────┐  │
│  │  CommandHandler  │   │  File Assemblers  │   │  TIA_V20        │  │
│  │                 │   │                  │   │  (Openness API) │  │
│  │ Route → SCL     │──▶│ • SCLGenerator   │──▶│                 │  │
│  │       → HMI     │   │ • HmiGenerator   │   │ • Import blocks │  │
│  │       → CWC     │   │ • CwcGenerator   │   │ • Import tags   │  │
│  │       → Agent   │   │ • SclCorrector   │   │ • Draw screens  │  │
│  └─────────────────┘   └──────────────────┘   │ • Compile       │  │
│                                                │ • Download      │  │
│                                                └─────────────────┘  │
└──────────────────────────────────────────────────────────────────┬──┘
                                                                   │
                               ┌───────────────────────────────────▼──┐
                               │      Siemens TIA Portal V17+          │
                               │   (S7-1200 / S7-1500 / WinCC Unified) │
                               └───────────────────────────────────────┘
```

### IPC Protocol

All inter-process communication uses **single-line JSON over stdin/stdout**. The C# layer serializes a request payload, writes one line to the Python process's stdin, and reads exactly one response line from stdout. This line-delimited protocol enables low-latency bidirectional exchange without HTTP server overhead, while remaining fully compatible with OT network security policies (no open ports, no outbound traffic beyond the Gemini API endpoint).

---

## ✨ Key Features

| Feature | Description |
|---|---|
| **SCL Code Generation** | Generates IEC 61131-3 compliant Function Blocks, Functions, Organization Blocks, and Data Blocks from natural language specifications |
| **HMI Screen Generation** | Produces WinCC Unified screen configurations for 25+ element types (tanks, valves, motors, trends, alarms) with correct tag bindings |
| **CWC Package Generation** | Builds complete `{GUID}.zip` Custom Web Control packages (Siemens `mver 1.2.0`) with generated HTML/CSS/JS and static Siemens assets |
| **PLC Tag Automation** | Extracts and allocates global tag addresses (`%I`, `%Q`, `%M`) with automatic memory alignment and CSV export for TIA Portal import |
| **Triple-Path RAG** | Semantic retrieval from three domain-specific ChromaDB collections (`iec_standard_kb`, `hmi_standard_kb`, `cwc_standard_kb`) with metadata filtering |
| **Sliding Window Memory** | SQLite-backed session history with configurable context window (default: 10 turns) for multi-turn engineering conversations |
| **Multi-Provider LLM** | Automatic API key detection — supports Google Gemini (`AIza…`), Anthropic Claude (`sk-ant-…`), and OpenAI (`sk-…`) from a single key field |
| **Agent Mode** | LangGraph-powered automation agent that plans and executes multi-step TIA Portal workflows (create device → generate code → import → compile → download) |
| **SCL Syntax Corrector** | Post-processing layer that normalizes VAR block declarations, fixes direction casing, merges extracted variables, and strips invalid keywords before file assembly |
| **Review System** | WebView2-embedded Monaco Editor for SCL review, JSONEditor for HMI screen inspection, and live CWC zip preview with mock WebCC runtime injection |

---

## 🚀 Getting Started

### Prerequisites

Before proceeding, ensure the following are installed and configured on your Windows machine:

| Requirement | Version | Notes |
|---|---|---|
| Windows OS | 10 / 11 (64-bit) | Required for TIA Portal and Openness API |
| Siemens TIA Portal | V17 or later | Professional edition with WinCC Unified |
| Python | 3.11.x | **Do not use 3.12+** — ChromaDB and sentence-transformers have known compatibility issues |
| .NET Framework | 4.8 | Included in Windows 10/11; required for C# middleware |
| Visual Studio | 2022 (Community or higher) | Must be launched as Administrator — see C# Setup |
| Git | Latest | For cloning the repository |

> ⚠️ **TIA Portal User Group** — Before any Openness API operation will succeed, your Windows user account **must** be a member of the `Siemens TIA Openness` local user group. To add yourself:
> 1. Open **Computer Management** → **Local Users and Groups** → **Groups**
> 2. Double-click `Siemens TIA Openness`
> 3. Click **Add** and enter your Windows username
> 4. **Log out and back in** for the group membership to take effect

---

### 🐍 Python Setup — AI Generative Engine

> ⚠️ **Conda/Miniconda Warning** — If you have Conda or Miniconda installed, you **must deactivate all active environments** before proceeding. Running `pip install` inside a Conda base environment will corrupt the Conda package manager and may cause irreversible environment conflicts. Run `conda deactivate` (repeat until your prompt shows no environment prefix) before continuing.

**1. Clone the repository**

```bash
git clone https://github.com/your-org/tia-portal-copilot.git
cd tia-portal-copilot
```

**2. Create a clean virtual environment**

```bash
cd AI_Backend
python -m venv env
```

**3. Activate the virtual environment**

```bash
# Windows CMD
env\Scripts\activate.bat

# Windows PowerShell
env\Scripts\Activate.ps1

# If PowerShell blocks execution, run first:
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

**4. Upgrade pip before installing dependencies**

> ⚠️ An outdated `pip` will silently install incorrect package versions for several ML dependencies. This step is mandatory.

```bash
python -m pip install --upgrade pip
```

**5. Install all dependencies**

```bash
pip install -r requirements.txt
```

**6. Configure API keys**

Create a `.env` file in `AI_Backend/` — **never commit this file to version control**:

```bash
# AI_Backend/.env
GEMINI_API_KEY=AIza...your_key_here
# Optional: add multiple keys for rotation
# GEMINI_API_KEY_2=AIza...
# OPENAI_API_KEY=sk-...
# ANTHROPIC_API_KEY=sk-ant-...
```

Alternatively, add your keys directly to `app_secrets.py`:

```python
# AI_Backend/app_secrets.py
list_keys_gemini = [
    "AIza...key_1",
    "AIza...key_2",   # optional — enables key rotation
]
```

**7. Ingest the knowledge base**

This step embeds the Siemens engineering heuristics into ChromaDB. Run it once, and again whenever you modify the `.md` knowledge base files.

```bash
python ingest.py all
```

Expected output:
```
[INGEST] iec_standard_kb   → 47 chunks embedded
[INGEST] hmi_standard_kb   → 38 chunks embedded
[INGEST] cwc_standard_kb   → 29 chunks embedded
[INGEST] All collections ready.
```

---

### 🔷 C# Setup — Execution Middleware

> ⚠️ **Administrator Requirement** — Visual Studio and the compiled `Translator_CLI.exe` **must run as Administrator**. The `app.manifest` is intentionally configured with `<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />` for two reasons:
> - Writing to `HKEY_LOCAL_MACHINE` to register the application in the TIA Portal Openness whitelist requires elevated privileges
> - The Openness API itself requires the calling process to have Administrator rights to attach to the TIA Portal process
>
> Right-click `devenv.exe` → **Run as administrator**, or set this permanently via the Visual Studio shortcut properties.

**1. Open the solution**

```
Translator_CLI/Translator_CLI.sln
```

**2. Restore NuGet packages**

Visual Studio will prompt automatically. If not:

```
Tools → NuGet Package Manager → Manage NuGet Packages for Solution → Restore
```

Key packages:
- `Siemens.Engineering` (V17+ Openness API — must be sourced from your TIA Portal installation)
- `Newtonsoft.Json` v13.0.4
- `ClosedXML` v0.105.0
- `Microsoft.Web.WebView2` v1.0+

> ⚠️ **Siemens.Engineering.dll** — This assembly is not on NuGet. It ships with TIA Portal. Add it as a local reference from:
> `C:\Program Files\Siemens\Automation\Portal V1x\PublicAPI\V1x\Siemens.Engineering.dll`
> Adjust the version path to match your TIA Portal installation.

**3. Set AI Backend path**

`AiEngine.cs` auto-discovers the Python backend by walking up the directory tree to find the `AI_Backend/` folder. The default structure expected is:

```
tia-portal-copilot/
├── AI_Backend/           ← Python engine lives here
│   ├── main.py
│   ├── env/              ← virtualenv
│   └── chroma_db_data/
└── Translator_CLI/       ← C# solution lives here
    └── Translator_CLI.sln
```

If your layout differs, update the `maxDepth` walk-up logic in `AiEngine.InitializePaths()`.

**4. Build and run**

```
Build → Build Solution  (Ctrl+Shift+B)
Debug → Start Debugging (F5)        ← runs as Administrator via app.manifest
```

On first run, the application automatically registers itself in the TIA Portal Openness Registry whitelist via SHA-256 hash. No manual registry editing is required.

---

## 💻 Usage

Once both modules are running, interact via the CLI prompt:

```
UserName-TIACopilot-[DEV] >
```

### AI Code Generation

```bash
# Generate a Function Block for motor control
chat fb "Create an FB for a 3-phase induction motor with start/stop/reset, 
         running time counter, and thermal overload protection"

# Generate an Organization Block wiring existing FBs
chat ob "Create OB1 calling FB_MotorControl for Pump_1, Pump_2, and Pump_3
         using tags from the loaded tag table"

# Generate a WinCC Unified HMI screen
chat scada "Create a process screen with 2 tanks, 4 pumps, level sensors,
            control valves, and a faceplate panel for each pump"

# Generate a Custom Web Control
chat cwc "Create a real-time circular gauge control with min/max/setpoint
          indicators, bound to a REAL PLC tag"
```

### Session and Context Management

```bash
# Load physical I/O tags from Excel or CSV
chat load-tags "C:\Projects\Station_1\IO_List.xlsx"

# Load project specification document
chat load-spec "C:\Projects\Station_1\Functional_Spec.docx"

# Manage conversation sessions
chat session          # open session manager
chat status           # show current session state
chat check-data       # preview loaded spec content
chat clear-data       # clear spec and session context
```

### TIA Portal Integration

```bash
# Connect to a running TIA Portal instance
tia connect

# Open or create a project
tia open "C:\TIA_Projects\Station_1.ap19"

# Import generated code directly into TIA Portal
tia fb                # import latest generated FB
tia ob                # import latest generated OB
tia tag-plc           # import PLC tag table CSV
tia draw              # draw HMI screen from generated JSON
tia cwc-deploy        # deploy CWC zip to project CustomControls

# Compile and download
tia compile sw        # compile software blocks
tia download          # download to PLC (prompts for network adapter)
tia run               # set PLC to RUN mode
```

### Agent Mode — Autonomous Workflow

```bash
# Execute a full multi-step workflow autonomously
agent "Connect to TIA Portal, create a CPU 1515-2 PN at 192.168.0.1,
           generate FB_PumpControl and OB1, import all files, compile
           software, and save the project"
```

The agent plans the full action sequence, executes each step via the Openness API, and reports results after each operation.

### Review Generated Files

```bash
# Open any generated file in the review interface
chat view             # opens file picker
chat view "Generated_Files\FB_PumpControl.scl"    # SCL in Monaco Editor
chat view "Generated_Files\Main_Process.json"      # HMI screen in JSONEditor
chat view "Generated_Files\{GUID}.zip"             # CWC live browser preview
```

---

## 📁 Project Structure

```
AI-Driven Engineering Platform/
│
├── AI_Backend/
│   ├── main.py                    # Request router, RAG pipeline, LLM invocation
│   ├── ingest.py                  # Offline KB ingestion into ChromaDB
│   ├── memory.py                  # SQLite session history management
│   ├── agent_core.py              # LangGraph automation agent
│   ├── app_secrets.py             # API key pool and cooldown management
│   ├── requirements.txt
│   ├── data/
│   │   ├── scl_siemens_base.md    # IEC 61131-3 SCL knowledge base
│   │   ├── hmi_siemens_base.md    # WinCC Unified HMI knowledge base
│   │   ├── cwc_siemens_base.md    # Custom Web Control knowledge base
│   │   ├── siemensplc_output_schema.json
│   │   ├── siemenshmi_output_schema.json
│   │   └── cwc_output_schema.json
│   └── chroma_db_data/            # Persisted vector store
│
└── Translator_CLI/
    ├── Program.cs                 # CLI shell and TIA command router
    ├── CommandHandler.cs          # AI response routing and file assembly dispatch
    ├── AiEngine.cs                # Python subprocess IPC manager
    ├── FileFormatter.cs           # SCL / HMI / CWC generators and normalizers
    ├── TIA_V20.cs                 # TIA Portal Openness API integration layer
    ├── ReviewWindow.cs            # WebView2 file review interface
    ├── agent_core/
    │   └── CommandHandler_Agent.cs
    ├── cwc_assets/                # Static Siemens CWC assets (webcc.min.js, etc.)
    ├── review_assets/             # Monaco Editor and JSONEditor assets
    └── Generated_Files/           # Output directory for all AI-generated artifacts
```

---

## ⚙️ Configuration Reference

| File | Key Setting | Description |
|---|---|---|
| `app_secrets.py` | `list_keys_gemini` | Gemini API key pool for load balancing and rotation |
| `app_secrets.py` | `CHROMA_DB_PATH` | Path to ChromaDB persistence directory |
| `app_secrets.py` | `enforce_cooldown(min_seconds)` | Minimum interval between API calls (default: 5s) |
| `memory.py` | `window_size=10` | Number of conversation turns retained in sliding window |
| `main.py` | `model="gemini-3.5-flash"` | Active LLM model identifier |
| `AiEngine.cs` | `maxDepth = 5` | Directory walk-up depth to locate `AI_Backend/` |

---

## 🔒 Security Considerations

- **API Keys** — Store keys in `app_secrets.py` or a `.env` file. Never commit credentials to version control. Add `app_secrets.py` and `.env` to `.gitignore`.
- **Administrator Privileges** — The application requires and requests Administrator rights explicitly via `app.manifest`. Review the source code before granting elevated execution to any binary.
- **OT Network Isolation** — The only external network traffic this application generates is HTTPS to the configured LLM API endpoint. All other operations (ChromaDB, SQLite, TIA Portal IPC) are local. The system is compatible with OT environments where outbound internet access is restricted to whitelisted HTTPS endpoints.
- **Code Review** — All AI-generated SCL code must be reviewed by a qualified automation engineer before execution on physical hardware. Treat generated code as a first draft, not a certified deliverable.

---
