import sys
import json
import os
import time
import logging
import warnings
import re

os.environ["CHROMA_TELEMETRY_IMPL"] = "none"
warnings.filterwarnings("ignore")
logging.getLogger("chromadb").setLevel(logging.ERROR)
logging.getLogger("sentence_transformers").setLevel(logging.ERROR)
logging.getLogger("langchain").setLevel(logging.ERROR)

from langchain_huggingface import HuggingFaceEmbeddings
import memory
import app_secrets

# ─────────────────────────────────────────────────────────────────
# GLOBAL SINGLETON CACHES — Initialize once per Python process
# ─────────────────────────────────────────────────────────────────
_embeddings_cache = None
_persistent_client_cache = None

def get_embeddings():
    global _embeddings_cache
    if _embeddings_cache is None:
        # Resolve model path — works both in dev and PyInstaller frozen exe
        if getattr(sys, 'frozen', False):
            # Running as compiled exe — model is next to the exe
            base = os.path.dirname(sys.executable)
        else:
            # Running as script — model is in AI_Backend/models/
            base = os.path.dirname(os.path.abspath(__file__))

        local_model_path = os.path.join(base, "models", "minilm")

        if os.path.isdir(local_model_path):
            # Local model found — fully offline, no Hub ping
            os.environ["HF_HUB_OFFLINE"] = "1"
            os.environ["TRANSFORMERS_OFFLINE"] = "1"
            os.environ["HUGGINGFACEHUB_API_TOKEN"] = "offline"
            _embeddings_cache = HuggingFaceEmbeddings(model_name=local_model_path)
        else:
            # Fallback — download from Hub (first run or model folder missing)
            print("[WARN] Local model not found, downloading from HuggingFace Hub...",
                  file=sys.stderr, flush=True)
            _embeddings_cache = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")

    return _embeddings_cache

def get_persistent_client():
    """Lazily initialize and cache chromadb persistent client (avoid reopening DB per request)."""
    global _persistent_client_cache
    if _persistent_client_cache is None:
        import chromadb
        _persistent_client_cache = chromadb.PersistentClient(path=app_secrets.CHROMA_DB_PATH)
    return _persistent_client_cache

# ─────────────────────────────────────────────────────────────────

def get_system_prompt_part1():
    try:
        with open("data/scl_siemens_base.md", "r", encoding="utf-8") as f:
            return f.read().split("# [PART 2] RAG_CONTEXT")[0]
    except:
        return "You are an expert IEC 61131-3 Programmer."

def get_hmi_system_prompt():
    try:
        with open("data/hmi_siemens_base.md", "r", encoding="utf-8") as f:
            return f.read().split("# [PART 2] RAG_CONTEXT")[0]
    except:
        return "You are an expert Siemens WinCC Unified HMI screen designer."

def get_cwc_system_prompt():
    try:
        with open("data/cwc_siemens_base.md", "r", encoding="utf-8") as f:
            return f.read().split("# [PART 2] RAG_CONTEXT")[0]
    except:
        return "You are an expert Siemens WinCC Unified Custom Web Control developer."

def get_output_schema(target_block_type="AUTO"):
    if target_block_type == "HMI_SCREEN":
        schema_file = "data/siemenshmi_output_schema.json"
        fallback = '{"screen_info": {"name": "AI_Screen", "width": 1920, "height": 1080}, "items": [], "global_tags": []}'
    elif target_block_type == "CWC_SCREEN":
        schema_file = "data/cwc_output_schema.json"
        fallback = '{"cwc_info": {"name": "AI_Control", "displayname": "AI Control", "description": ""}, "properties": [], "events": [], "methods": [], "third_party_libs": [], "html_content": "", "js_content": "", "css_content": ""}'
    else:
        schema_file = "data/siemensplc_output_schema.json"
        fallback = """
        {
          "block_info": { "name": "FB_Standard", "type": "FUNCTION_BLOCK", "description": "Standard Block" },
          "interface": [
            { "name": "i_Enable", "type": "BOOL", "direction": "VAR_INPUT", "description": "Enable input" },
            { "name": "q_Ready", "type": "BOOL", "direction": "VAR_OUTPUT", "description": "Ready output" },
            { "name": "stat_Timer", "type": "TON", "direction": "VAR", "description": "Internal Timer" }
          ],
          "body_code": "#stat_Timer(IN := #i_Enable, PT := T#1s);\\n #q_Ready := #stat_Timer.Q;",
          "global_tags": [
            { "name": "TAG_StartBtn_01", "type": "BOOL", "comment": "Input" },
            { "name": "TAG_MotorSpeed_01", "type": "REAL", "comment": "Output" },
            { "name": "TAG_SystemFlag", "type": "BOOL", "comment": "Memory" } 
          ],
            "global_timers": []
        }
        """
    try:
        with open(schema_file, "r", encoding="utf-8") as f:
            return f.read()
    except:
        return fallback

def send_response(data_dict):
    output_bytes = (json.dumps(data_dict, ensure_ascii=False) + "\n").encode('utf-8')
    sys.stdout.buffer.write(output_bytes)
    sys.stdout.buffer.flush()
    os._exit(0)

# def send_response(data_dict):
#     output_bytes = (json.dumps(data_dict, ensure_ascii=False) + "\n").encode('utf-8')
#     sys.stdout.buffer.write(output_bytes)
#     sys.stdout.buffer.flush()

# def clean_json_response(text):
#     cleaned = text.strip()
#     if cleaned.startswith("```json"):
#         cleaned = cleaned.replace("```json", "", 1)
#     if cleaned.startswith("```"):
#         cleaned = cleaned.replace("```", "", 1)
#     if cleaned.endswith("```"):
#         cleaned = cleaned[:-3]
#     return cleaned.strip()ư

def clean_json_response(content_list):
    # Phòng hờ trường hợp hệ thống truyền nhầm một chuỗi String vào hàm này
    if isinstance(content_list, str):
        return clean_json_response(content_list)
    if not isinstance(content_list, list):
        return str(content_list).strip()
    extracted_fragments = []
    # Duyệt qua từng phần tử trong danh sách để bóc text an toàn
    for part in content_list:
        if isinstance(part, str):
            extracted_fragments.append(part)
        elif isinstance(part, dict):
            extracted_fragments.append(part.get("text", ""))
        elif hasattr(part, "text"):  # Đánh chặn cấu trúc Block Object của LangChain
            extracted_fragments.append(getattr(part, "text", ""))
        elif hasattr(part, "content"): # Dự phòng trường hợp là Message Object
            extracted_fragments.append(getattr(part, "content", ""))
    # Gộp toàn bộ các mảnh text lại thành một chuỗi duy nhất
    full_text = "".join(extracted_fragments)
    cleaned = full_text.strip()
    if cleaned.startswith("```json"):
        cleaned = cleaned.replace("```json", "", 1)
    if cleaned.startswith("```"):
        cleaned = cleaned.replace("```", "", 1)
    if cleaned.endswith("```"):
        cleaned = cleaned[:-3]
    return cleaned.strip()


# Fixing \n and \t in the body_code string to ensure they are interpreted correctly by the assembler
def normalize_body_code(obj):
    if isinstance(obj, str):
        return (obj
                .replace('\\r\\n', '\n')  # literal 4-char sequence \\r\\n → real newline
                .replace('\\n',    '\n')  # literal 2-char sequence \\n    → real newline
                .replace('\\t',    '\t')  # literal 2-char sequence \\t    → real tab
                .replace('\\r',    '')    # lone literal \\r               → discard
                )
    elif isinstance(obj, dict):
        return {k: normalize_body_code(v) for k, v in obj.items()}
    elif isinstance(obj, list):
        return [normalize_body_code(item) for item in obj]
    return obj

def detect_model_from_key(api_key: str):
    if not api_key or not api_key.strip():
        raise ValueError("API key is empty. Provide a valid key in USER mode.")

    key = api_key.strip()

    # ── Google Gemini ─────────────────────────────────────────────────────────
    if key.startswith("AIza"):
        from langchain_google_genai import ChatGoogleGenerativeAI
        os.environ["GOOGLE_API_KEY"] = key
        llm = ChatGoogleGenerativeAI(
            model="gemini-3.5-flash",
            temperature=0.1,
            convert_system_message_to_human=True,
            google_api_key=key,
            model_kwargs={"response_mime_type": "application/json"},
        )
        return llm, "Gemini (gemini-3.5-flash)"

    # ── Anthropic Claude ──────────────────────────────────────────────────────
    elif key.startswith("sk-ant-"):
        from langchain_anthropic import ChatAnthropic
        os.environ["ANTHROPIC_API_KEY"] = key
        llm = ChatAnthropic(
            model="claude-3-5-sonnet-20241022",
            temperature=0.1,
            api_key=key,
            max_tokens=8192,  # Claude requires explicit max_tokens
            max_retries=0,
        )
        return llm, "Anthropic (claude-3-5-sonnet-20241022)"

    # ── OpenAI ────────────────────────────────────────────────────────────────
    elif key.startswith("sk-"):
        from langchain_openai import ChatOpenAI
        os.environ["OPENAI_API_KEY"] = key
        llm = ChatOpenAI(
            model="gpt-4o-mini",
            temperature=0.6,
            api_key=key,
            model_kwargs={"response_format": {"type": "json_object"}},
            max_retries=0,
        )
        return llm, "OpenAI (gpt-4o-mini)"

    else:
        raise ValueError(
            f"Unrecognized API key format (first 12 chars): '{key[:12]}'\n"
            "Supported prefixes: 'AIza' (Gemini), 'sk-ant-' (Anthropic), 'sk-' (OpenAI)."
        )


def main():
        sys.stdin.reconfigure(encoding='utf-8-sig') 
        sys.stdout.reconfigure(encoding='utf-8')       
        try:
            # Đọc dữ liệu và gọt thêm một lần nữa cho chắc ăn
            input_raw = sys.stdin.read().strip().lstrip('\ufeff')
            if not input_raw: return

            request_data = json.loads(input_raw)
            if not input_raw: return

            # 🛠️ SỬA 3: Giải mã JSON (Đã dọn dẹp đoạn code trùng lặp của Đăng)
            request_data = json.loads(input_raw)
            user_query = request_data.get("query", "")
            session_id = request_data.get("session_id", "default")
            command_type = request_data.get("command", "chat") 
            context_code = request_data.get("context_code", "")
            user_tags = request_data.get("user_tags", "").strip()
            spec_text = request_data.get("spec_text", "").strip()
            target_block_type = request_data.get("target_block_type", "AUTO").upper()
            system_mode = request_data.get("system_mode", "USER").upper()
            custom_api_key = request_data.get("custom_api_key", "").strip()


            if system_mode == "DEV":
                CURRENT_KEY = app_secrets.get_next_api_key()
            else:
                CURRENT_KEY = custom_api_key

            os.environ["GOOGLE_API_KEY"] = CURRENT_KEY  

            # region XỬ LÝ LỆNH ĐẶC BIỆT (KHÔNG PHẢI CHAT) - LIST SESSIONS, RESET SESSION, UPDATE SPEC, CHECK SPEC
            if command_type == "list_sessions":
                sessions = memory.list_all_sessions()
                send_response({"status": "success", "sessions": sessions})
                return

            elif command_type == "create_session":
                memory.init_session(session_id)
                send_response({"status": "success", "message": f"Created {session_id}"})
                return

            elif command_type == "reset":
                memory.clear_session(session_id)
                send_response({"status": "success", "message": f"Session '{session_id}' cleared."})
                return

            # --- BƯỚC 2: XỬ LÝ LỆNH UPDATE SPEC ---
            elif command_type == "update_spec":
                from langchain_text_splitters import RecursiveCharacterTextSplitter

                try:
                    # Reuse global client and embeddings to avoid reload overhead
                    persistent_client = get_persistent_client()
                    embeddings = get_embeddings()
                    
                    # Delete old collection if it exists
                    try:
                        persistent_client.delete_collection("current_project_spec")
                    except Exception:
                        pass 
                    
                    if spec_text:
                        # Split spec into chunks
                        splitter = RecursiveCharacterTextSplitter(chunk_size=500, chunk_overlap=50)
                        chunks = splitter.split_text(spec_text)
                        
                        # Create collection and upsert documents directly (bypass LangChain wrapper overhead)
                        collection = persistent_client.create_collection(name="current_project_spec")
                        
                        # Batch embed and upsert
                        chunk_embeddings = embeddings.embed_documents(chunks)
                        collection.upsert(
                            documents=chunks,
                            embeddings=chunk_embeddings,
                            ids=[f"chunk_{i}" for i in range(len(chunks))]
                        )
                        msg = f"Chunked and loaded {len(chunks)} chunks Spec into Vector DB."
                    else:
                        msg = "Deleted old Spec. Current system has no Spec constraints."
                        
                    send_response({"status": "success", "message": msg})
                    return
                except Exception as e:
                    send_response({"status": "error", "message": f"Error loading Spec: {str(e)}"})
                    return
                
            elif command_type == "check_spec":
                try:
                    # Reuse global client to avoid DB reopening overhead
                    persistent_client = get_persistent_client()
                    
                    try:
                        collection = persistent_client.get_collection("current_project_spec")
                        results = collection.get(limit=1000)  # Limit retrieval for memory efficiency
                        docs = results.get("documents", [])
                        
                        if not docs:
                            msg = "No current spec found. The system is empty."
                        else:
                            # Show first 10 chunks, then indicate if there are more
                            displayed_docs = docs[:10]
                            preview_text = "\n\n--- [CHUNK NEXT] ---\n\n".join(displayed_docs)
                            remaining = len(docs) - len(displayed_docs)
                            if remaining > 0:
                                preview_text += f"\n\n... and {remaining} more chunks"
                            msg = f"Found {len(docs)} chunks in current Spec.\n\n[CURRENT SPEC CONTENT]:\n{preview_text}"
                    except Exception:
                        msg = "No current Spec collection found. The system is completely empty."   
                    send_response({"status": "success", "message": msg})
                    return
                
                except Exception as e:
                    send_response({"status": "error", "message": f"Error reading Spec: {str(e)}"})
                    return
                    
            # --- LỆNH DỌN DẸP VECTOR DB (SPEC) ---
            elif command_type == "clear_spec":
                try:
                    # Reuse global client to avoid DB reopening overhead
                    persistent_client = get_persistent_client()
                    
                    try:
                        persistent_client.delete_collection("current_project_spec")
                        msg = "Deleted spec successful. Database collection cleared!"
                    except Exception:
                        # Collection doesn't exist — already empty
                        msg = "System is empty, no Spec to delete."

                    send_response({"status": "success", "message": msg})
                    return
                except Exception as e:
                    send_response({"status": "error", "message": f"Error deleting Spec: {str(e)}"})
                    return
            
            # endregion
            
            # region TRIPPLE-PATH RAG RETRIEVAL — parallel execution, direct ChromaDB client
            # ─────────────────────────────────────────────────────────────────────
            # SPEED RATIONALE:
            #   Old approach: LangChain Chroma wrapper → sequential kb then spec retrieval
            #   New approach: chromadb.PersistentClient directly + ThreadPoolExecutor
            #
            #   Gains:
            #   1. Skip LangChain Chroma wrapper overhead (~0.3s per collection open)
            #   2. kb_context and spec_context retrieved IN PARALLEL (concurrent I/O)
            #   3. Embedding computed ONCE, reused for both queries
            #   4. GLOBAL CACHES: embeddings & persistent_client reused across requests
            # ─────────────────────────────────────────────────────────────────────
            from langchain_google_genai import ChatGoogleGenerativeAI
            from langchain_openai import ChatOpenAI
            from langchain_anthropic import ChatAnthropic
            from concurrent.futures import ThreadPoolExecutor, as_completed

            # Reuse global cached embeddings and client to avoid reload overhead (~1-3s saved)
            embeddings = get_embeddings()
            persistent_client = get_persistent_client()

            # Embed the query ONCE — reused for all collection queries
            query_vector = embeddings.embed_query(user_query)

            kb_context   = ""
            spec_context = ""

            def _query_kb() -> str:
                """Retrieve top-k chunks from the type-specific knowledge base."""
                query_upper = user_query.upper()
                try:
                    if target_block_type == "CWC_SCREEN":
                        collection = persistent_client.get_collection("cwc_standard_kb")
                        k = 8
                        where = None

                    elif target_block_type == "HMI_SCREEN":
                        collection = persistent_client.get_collection("hmi_standard_kb")
                        k = 5
                        where = None
                        if any(w in query_upper for w in ["TANK","VALVE","MOTOR","PUMP","PIPE","SENSOR","INDICATOR"]):
                            where = {"type": {"$in": ["WIDGET", "LAYOUT"]}}
                        elif any(w in query_upper for w in ["TREND","ALARM","RECIPE","DIAGNOSIS","CHART"]):
                            where = {"type": {"$in": ["CONTROL", "SCREEN"]}}
                        elif any(w in query_upper for w in ["BUTTON","NAVIGATE","SCREEN","WINDOW"]):
                            where = {"type": {"$in": ["SCREEN", "CONTROL", "LAYOUT"]}}

                    else:  # SCL — FB / FC / OB
                        collection = persistent_client.get_collection("iec_standard_kb")
                        k = 8
                        where = None
                        if target_block_type in ["FB", "FC", "DB", "FUNCTION_BLOCK", "FUNCTION", "DATA_BLOCK"]:
                            where = {"type": {"$in": ["COMPONENT", "SYNTAX"]}}
                        elif target_block_type in ["OB", "ORGANIZATION_BLOCK"]:
                            where = {"type": {"$in": ["SYSTEM", "SYNTAX"]}}

                    results = collection.query(
                        query_embeddings=[query_vector],
                        n_results=k,
                        where=where,            # None = no filter (all types searched)
                        include=["documents"]
                    )
                    docs = results.get("documents", [[]])[0]
                    return "\n\n".join(docs)
                except Exception:
                    return ""  # KB not ingested yet — prompt still works without it

            def _query_spec() -> str:
                """Retrieve top-5 chunks from the current project spec collection."""
                try:
                    spec_col = persistent_client.get_collection("current_project_spec")
                    results = spec_col.query(
                        query_embeddings=[query_vector],
                        n_results=5,
                        include=["documents"]
                    )
                    docs = results.get("documents", [[]])[0]
                    return "\n\n".join(docs)
                except Exception:
                    return ""  # Spec not loaded — silently continue

            # Run both retrievals in parallel — saves ~0.5–1.5s on typical hardware
            with ThreadPoolExecutor(max_workers=2) as executor:
                future_kb   = executor.submit(_query_kb)
                future_spec = executor.submit(_query_spec)
                kb_context   = future_kb.result()
                spec_context = future_spec.result()

            # endregion
            
            # region Prompt assembly — branches on target_block_type
            block_type_constraint = ""
            if target_block_type not in ["AUTO", "HMI_SCREEN", "CWC_SCREEN"]:
                block_type_constraint = f"""
                ### HARD CONSTRAINT - FORCED BLOCK TYPE:
                You MUST set the "type" field to "{target_block_type}".
                Your code MUST be formatted according to the rules of a {target_block_type}.
                """

            user_tags_constraint = ""
            if user_tags and target_block_type in ["OB", "ORGANIZATION_BLOCK"]:
                user_tags_constraint = f"""
            ### 🎯 USER DEFINED I/O TAGS (STRICT WIRING DICTIONARY):
            The user has provided a specific list of physical I/O tags. 
            When you are writing code to call a Function Block (FB) or Function (FC) inside an OB, you MUST map the inputs/outputs ONLY to the tags listed below.
            DO NOT invent, fabricate, or guess new global tag names. Choose the most logically appropriate tag from this list based on its name and Data Type.
            When you are writing OB code, the I/O tags provided should be your ONLY source of global variables to interact with the outside world. You MUST declare them again in 'global_tags' field in your JSON output, following the exact tag names, data types and addresses as provided in the list below. If you need new tags for OB, you MUST only declare inside 'global_tags' field in your JSON output, and those tags MUST follow the same naming convention and be clearly described in their comment.
            
            [AVAILABLE TAGS]:
            {user_tags}
                """

            chat_history_str = memory.get_sliding_window_context(session_id, window_size=10)
            system_rules = get_system_prompt_part1()
            target_schema = get_output_schema(target_block_type)

            if target_block_type == "CWC_SCREEN":
                cwc_system_rules = get_cwc_system_prompt()

                cwc_tags_constraint = ""
                if user_tags:
                    cwc_tags_constraint = f"""
            ### 🎯 AVAILABLE PLC TAGS — USE THESE AS PROPERTY NAMES:
            The following tags exist on the PLC. When declaring properties in the "properties" array,
            name them after the relevant tags below. The same names MUST appear in your js_content
            when calling WebCC.onPropertyChanged and WebCC.Properties.
            DO NOT invent tag names that are not in this list.

            [AVAILABLE TAGS]:
            {user_tags}
                    """

                full_prompt = f"""
            {cwc_system_rules}

            {cwc_tags_constraint}

            ### 🛑 PROJECT OPERATIONAL REQUIREMENTS (MUST FOLLOW):
            {spec_context}

            ### 📚 CWC OBJECT REFERENCE (RETRIEVED FROM KNOWLEDGE BASE):
            Use these rules to select correct property types, event patterns, and UI element implementations.
            {kb_context}

            ### CHAT HISTORY:
            {chat_history_str}

            ### REQUIRED JSON OUTPUT SCHEMA:
            You MUST return JSON that EXACTLY matches this structure.
            Remove all "_comment" and "_comment_*" fields from your output — they are reference only.

            {target_schema}

            ### ⚙️ CRITICAL RULES (VIOLATIONS BREAK THE CONTROL):

            1. **WebCC.start() — WRITE IT COMPLETELY IN js_content:**
            - js_content must contain the FULL WebCC.start() call.
            - The contract object inside WebCC.start() MUST match your declared arrays:
                - methods: object with REAL function implementations (not empty stubs)
                - events: array of event name strings exactly as declared in "events"
                - properties: object with default values exactly as declared in "properties"
            - Pattern: WebCC.start(function(result){{ if(result){{ init(); WebCC.onPropertyChanged.subscribe(setProperty); }} }}, {{ methods:{{...}}, events:[...], properties:{{...}} }}, [], 10000);

            2. **NAME CONSISTENCY (CRITICAL — case-sensitive):**
            - Every name in "properties" array → used in WebCC.Properties.Name AND in the properties object inside WebCC.start()
            - Every name in "events" array → used in WebCC.Events.fire("Name") AND in events array inside WebCC.start()
            - Every name in "methods" array → implemented as a function in methods object inside WebCC.start()
            - One mismatch silently breaks TIA Portal tag binding.

            3. **PROPERTY TYPES:**
            - "boolean" → BOOL PLC tags. Default value must be false (not "false").
            - "number"  → INT, REAL, DINT tags. Default value must be a number (not string).
            - "string"  → STRING tags. Default value must be "" (empty string).

            4. **HTML STRUCTURE (MANDATORY LOAD ORDER):**
            - In <head>: webcc.min.js FIRST, then third-party libs, then styles.css
            - At END of <body>: code.js ONLY
            - No inline JS anywhere in html_content.
            - Give every interactive element a unique id attribute.

            5. **THIRD-PARTY LIBRARIES:**
            - Only declare in "third_party_libs" if user explicitly requests one OR the UI requires it.
            - Use filenames only (e.g. "gauge.min.js"). File must exist in cwc_assets/ folder.
            - If none needed: "third_party_libs": []
            - Load in html_content as: <script src='./js/gauge.min.js'></script>

            6. **DESIGN MODE GUARD (MUST INCLUDE):**
            - Inside the WebCC.start() success callback, check design mode first:
                if (WebCC.isDesignMode) {{ showPlaceholder(); return; }}
            - showPlaceholder() renders a static labeled preview of the control.

            7. **RESPONSIVE SIZING:**
            - body: width:100%; height:100%; overflow:hidden; margin:0
            - Use %, flex, or canvas resize logic — no hardcoded px on outer containers.

            8. **CSS STYLE:**
            - Default: dark industrial (#1a1a2e background, high contrast text).
            - Unless the user requests a specific color scheme.

            9. **cwc_info NAME FIELD:**
            - PascalCase, underscores for spaces. Example: "Tank_Level_Monitor".
            - This becomes the control's display name in TIA Portal Toolbox.

            ### USER REQUEST:
            {user_query}

            GENERATE JSON ONLY. No markdown, no explanation, no code fences.
            """
            elif target_block_type == "HMI_SCREEN":
                hmi_system_rules = get_hmi_system_prompt()

                hmi_tags_constraint = ""
                if user_tags:
                    hmi_tags_constraint = f"""
            ### 🎯 AVAILABLE PLC TAGS — STRICT BINDING DICTIONARY:
            The following tags exist on the PLC. When you bind an HMI object to a tag, you MUST
            choose ONLY from this list. Do NOT invent or fabricate tag names.
            Map each object to the most logically appropriate tag based on its name and data type and same address as much as possible. If no suitable tag exists, you can invent ONE new tag but it MUST follow the naming convention and be clearly described in its comment.
            You have to define again all the used tags in the "global_tags" array in your JSON output, with their name, type, address and comment — even if they are already defined in the PLC tag list. This is necessary for the HMI assembler to know which tags to create on the HMI side and how to bind them.

            [AVAILABLE TAGS]:
            {user_tags}
                    """

                full_prompt = f"""
            {hmi_system_rules}

            {hmi_tags_constraint}

            ### 🛑 PROJECT OPERATIONAL REQUIREMENTS (MUST FOLLOW):
            This is the project spec. Every screen object and tag binding MUST align with these rules.
            {spec_context}

            ### 📚 HMI OBJECT REFERENCE (RETRIEVED FROM KNOWLEDGE BASE):
            Use these rules to select correct object types, subtypes, behaviors, and field names.
            {kb_context}

            ### CHAT HISTORY:
            {chat_history_str}

            ### REQUIRED JSON OUTPUT SCHEMA:
            You MUST return JSON that EXACTLY matches this structure. Do not add, remove, or rename any keys.
            Every item in the "items" array MUST have at minimum: "name", "type".
            Remove the "_comment_*" fields — those are for your reference only, do NOT include them in output.

            {target_schema}

            ### ⚙️ CRITICAL RULES (MUST FOLLOW — VIOLATIONS WILL BREAK THE ASSEMBLER):

            1. **LOGICAL JSON ONLY — NO PHYSICAL DATA:**
            - Do NOT include pixel coordinates (Left, Top, Width, Height).
            - Do NOT include LibraryPath strings.
            - Do NOT write any JavaScript (ColorScript, KeyDown/KeyUp script strings).
            - The C# assembler handles all of the above. Your job is intent and tag binding only.

            2. **TAG BINDING RULE:**
            - Use "bind_tag" for the primary tag that drives the object's state or value.
            - If no tags are provided, invent logical tag names that match the process described.
            - For TrendControl, use "trend_tag" instead of "bind_tag".
            - AlarmControl, FunctionTrendControl, SystemDiagnosisControl do NOT need a tag.

            3. **BEHAVIOR KEYWORDS — ONLY USE EXACT STRINGS:**
            - "fill_level"       → analog tag drives a visible fill (use for Tank, Bar)
            - "color_on_status"  → boolean tag drives green/red color change (use for Valve, Motor, Rectangle, Circle)
            - Do NOT invent other behavior keywords.

            4. **BUTTON RULES:**
            - Momentary button (START/STOP/RESET): include "keydown_write" and "keyup_write" with tag + value.
            - Navigation button (screen switch): include "navigate_to" with exact target screen name. No write fields.
            - Never combine both patterns on the same button.

            5. **SUBTYPE RULE:**
            - Valve: "ControlValve" or "GateValve"
            - Motor: "Motor2" (horizontal) or "Motor9Vertical" (vertical)
            - Pipe: "PipeHorizontal" or "PipeVertical"
            - Tank, Rectangle, Circle: no subtype needed.

            6. **HINT FIELD:**
            - Every object MUST have a "hint" field describing its intended zone and role.
            - Format: "<zone>, <role>". Example: "left sidebar, START button for conveyor pump"
            - Zones: "center process area", "left sidebar", "top status bar", "right indicator column",
                "bottom navigation bar", "top-left monitoring panel", "top-right monitoring panel"

            7. **NAMING CONVENTION:**
            - Use underscore-separated Vietnamese or English names. No spaces.
            - Example: "Bon_Chua_Chinh", "Nut_START", "Van_Cap_Vao_01"

            8. **GLOBAL TAGS:**
            - If the user's request implies new HMI-only tags (not in the PLC list), declare them in "global_tags".
            - Each entry needs: "name" (string), "type" (BOOL/INT/REAL), "comment" (purpose description).
            - If no new tags are needed, return "global_tags": [].

            9. **SCREEN INFO:**
            - Always fill "screen_info" with a meaningful "name", and default width/height of 1920x1080
                unless the user specifies otherwise.

            ### USER REQUEST:
            {user_query}

            GENERATE JSON ONLY. No markdown, no explanation, no code fences.
            """

            else:
                full_prompt = f"""
            {system_rules}

            {user_tags_constraint}

            {block_type_constraint}
            
            ### 🛑 HARD CONSTRAINTS & OPERATIONAL LOGIC (MUST FOLLOW):
            # Đây là Spec dự án. BẠN BẮT BUỘC PHẢI DỰA VÀO ĐÂY ĐỂ VIẾT LOGIC.
            {spec_context}
            
            ### 📚 REFERENCE STANDARDS (GUIDELINES ONLY):
            # Đây là tiêu chuẩn IEC. CHỈ DÙNG ĐỂ THAM KHẢO CÚ PHÁP.
            {kb_context}
            
            ### CHAT HISTORY:
            {chat_history_str}

            ### CURRENT FILE CONTEXT (The code user is working on):
            {context_code}
            
            ### REQUIRED JSON SCHEMA:
            You MUST strictly follow this JSON structure. Do not change keys or nesting.
            
            {target_schema}
            
            **CRITICAL RULES (MUST FOLLOW):**
            1. **NAMING CONVENTION:** Use Hungarian Notation (i_, q_, iq_, stat_, temp_).
            2. **JSON MAPPING (CRITICAL):** - ALL variables MUST be defined ONLY in the "interface" array.
            - The "body_code" string is STRICTLY for executable logic ONLY. YOU MUST NOT write `VAR`, `END_VAR`, `VAR_TEMP`, or `BEGIN` inside "body_code".
            3. **SIEMENS SCL SYNTAX (FB/FC):** In "body_code", you MUST prefix ALL local variables with `#` (e.g., `#q_Motor := #stat_Timer.Q;`).
            4. **OB STRICT RULES & ANTI-OVERRIDE:** - In an ORGANIZATION_BLOCK (OB), you CANNOT declare instances in the interface. 
            - To call an FB, you MUST use its Global Data Block name in double quotes. 
            - **CRITICAL FORMAT:** The DB name MUST ALWAYS be formatted exactly as `"Inst_<FB_Name>__<Instance_Name>"`. 
            - **EXAMPLE:** If the FB is named "FB_WaterPump" and the user wants to call it "Pump 1", you MUST write `"Inst_FB_WaterPump__Pump1"`. 
            - You MUST use a DOUBLE UNDERSCORE (`__`) to separate the FB_Name and the Instance_Name. DO NOT use a single underscore here. NEVER drop the FB type from the DB name.
            - DO NOT use the `#` prefix for Global DB calls and Global Tags in OB. ONLY use `#` for local variables inside FB/FC body_code.
            - **GLOBAL TAG WIRING RULE:** When wiring inputs/outputs to the FB in an OB, if you create new global tags, you MUST prefix them with `TAG_` and put them in double quotes (e.g., `"TAG_StartBtn_1"`, `"TAG_MainConveyor_Out"`).
            5. **STATE MACHINE & TIMERS (CRITICAL):** NEVER call the same Timer (TON/TOF) or Counter (CTU/CTD) multiple times inside IF or CASE statements. You MUST call them EXACTLY ONCE at the end of the "body_code". Use internal flags to trigger their inputs.
            6. **MATH & ANALOG RULE (CRITICAL):** NORM_X, SCALE_X, ABS, MIN, MAX are built-in functions. DO NOT declare them in the interface (VAR). Call them directly and assign their return value (e.g., `#temp_Real := SCALE_X(MIN:=0.0, VALUE:=#temp_Norm, MAX:=100.0);`). ALWAYS use 'VALUE' parameter, NOT 'IN'.
            7. **GLOBAL TAG DECLARATION:** If you generate an OB and create any global tags (with `TAG_` prefix), 
            you MUST list ALL of them inside the `"global_tags"` JSON array with their "type", "address" and "comment". If new tags created for OB, you MUST not let their address be the same as any existing PLC tags in the user-provided list. Invent new addresses for them, but follow the same naming convention.
            FORBIDDEN types in global_tags: TON, TOF, TP, TONR, R_TRIG, F_TRIG, CTU, CTD, CTUD, or any FB type.
            ONLY use plain data types: BOOL, INT, REAL, DINT, WORD, DWORD, BYTE, STRING, TIME.
            Timers and triggers exist ONLY inside FB VAR sections, accessed via Instance DB in OB body_code.
            8. **GLOBAL TAG COMMENT:** Inside the "global_tags" array, you MUST add a "comment" field for each tag. Evaluate how the tag is wired in the OB: if it's wired to an input, label it "Input"; if to an output, label it "Output"; otherwise label it "Memory".
            9. **GLOBAL TIMERS (IEC_TIMER) - CRITICAL:** If the User Spec explicitly requests a Global Timer (IEC_TIMER) instead of a local static timer, you MUST use the global DB syntax (e.g., `"T10S".TON(IN:=..., PT:=...);`). 
            Do NOT declare this timer in the "interface" (VAR). 
            You MUST list its exact name in the `"global_timers"` JSON array (e.g., `"global_timers": ["T10S"]`).
            If no global timers are requested, return `"global_timers": []`.
            10. **VERSION CONTROL NAMING (CRITICAL):** 
                - If "CURRENT FILE CONTEXT" is EMPTY: You are creating a NEW block. Give it a clean name (e.g., "FB_ValveController"). DO NOT add any version suffix.
                - If "CURRENT FILE CONTEXT" HAS DATA: You are MODIFYING an existing block. You MUST extract its original name and INCREMENT its version suffix in your new JSON output.
                * Rule A: If the original name has NO version, append "_V2" (e.g., "FB_Pump" -> "FB_Pump_V2").
                * Rule B: If the original name ends in "_V[N]", replace it with "_V[N+1]" (e.g., "FB_Pump_V2" -> "FB_Pump_V3", "FC_Math_V9" -> "FC_Math_V10").
            11. **ORGANIZATION BLOCK (OB) NAMING & METADATA RULE (CRITICAL):**
                - **JSON CLEAN NAME:** The "name" field MUST ALWAYS be a clean string WITHOUT ANY literal escaped quotes. (e.g., `"name": "Main_Loop"`, NEVER `"name": "\"Main_Loop\""`).
                - **For Main Loop (Program Cycle):** The name can be auto-generated (e.g., `"name": "PID_Main"`). DO NOT add @CyclicTime.
                - **For Timed Loops (Cyclic Interrupt):** To be recognized by TIA Portal, the name MUST be exactly between "OB30" and "OB35" (e.g., `"name": "OB30"`).
                - **CYCLIC TIME COMMENT:** For Cyclic Interrupts ONLY, you MUST include the metadata comment `// @CyclicTime: <Value>µs` ANYWHERE inside the "body_code" string.

            ### USER REQUEST:
            {user_query}
            
            GENERATE JSON ONLY.
            """
            # endregion
            
            # ── Model auto-detection ──────────────────────────────────────────────
            # DEV mode: always Gemini via app_secrets key pool
            # USER mode: detect provider from custom_api_key prefix
            if system_mode == "DEV":
                dev_key = app_secrets.get_next_api_key()
                os.environ["GOOGLE_API_KEY"] = dev_key
                from langchain_google_genai import ChatGoogleGenerativeAI
                llm = ChatGoogleGenerativeAI(
                    model="gemini-3.5-flash",
                    temperature=0.1,
                    convert_system_message_to_human=True,
                    google_api_key=dev_key,
                    model_kwargs={"response_mime_type": "application/json"},
                )
                provider_name = "Gemini (gemini-3.5-flash) [DEV]"
            else:
                try:
                    llm, provider_name = detect_model_from_key(custom_api_key)
                except ValueError as e:
                    send_response({"status": "error", "message": str(e)})
                    return

            print(f"[AI] Using: {provider_name}", file=sys.stderr, flush=True)

            # ── Cooldown guard (DEV/Gemini only) ──────────────────────────────────
            if system_mode == "DEV":
                app_secrets.enforce_cooldown(min_seconds=5.0)

            # ── Retry logic ───────────────────────────────────────────────────────
            # Only 503 and RPM are safe to retry — neither charges RPD quota.
            # On retry: DEV mode rotates to next key from pool.
            #           USER mode re-detects from the same custom_api_key (provider unchanged).
            MAX_RETRIES = 3
            response    = None
            last_hint   = ""
            err_type    = "OTHER"

            for attempt in range(MAX_RETRIES + 1):
                try:
                    response = llm.invoke(full_prompt)
                    if system_mode == "DEV":
                        app_secrets.record_api_call()
                    break

                except Exception as api_exc:
                    err_type = app_secrets.classify_api_error(api_exc)

                    if err_type == "RPD":
                        last_hint = (
                            "Daily quota (RPD) exhausted on this key. Rotate keys or wait."
                            if system_mode == "DEV" else
                            "Your API Key has exhausted its daily quota."
                        )
                        break

                    if err_type == "AUTH":
                        last_hint = (
                            "API key rejected (401/403). Check app_secrets.py."
                            if system_mode == "DEV" else
                            "Your Custom API Key is invalid or expired."
                        )
                        break

                    if err_type == "OTHER":
                        last_hint = str(api_exc)
                        break

                    # ── Retryable: 503 and RPM ────────────────────────────────────
                    if attempt == MAX_RETRIES:
                        last_hint = (
                            f"Servers still overloaded after {MAX_RETRIES} retries. Wait and retry."
                            if err_type == "503" else
                            f"Rate limit (RPM) still hit after {MAX_RETRIES} retries. Wait ~60s."
                        )
                        break

                    # Rotate key and rebuild LLM for next attempt
                    if system_mode == "DEV":
                        new_key = app_secrets.get_next_api_key()
                        os.environ["GOOGLE_API_KEY"] = new_key
                        llm = ChatGoogleGenerativeAI(
                            model="gemini-3.5-flash",
                            temperature=0.1,
                            convert_system_message_to_human=True,
                            google_api_key=new_key,
                            model_kwargs={"response_mime_type": "application/json"},
                        )
                        print_action = f"Rotated to key ...{new_key[-8:]}"
                    else:
                        # USER mode: same key, re-detect (provider unchanged, just rebuild object)
                        llm, _ = detect_model_from_key(custom_api_key)
                        print_action = "Holding custom key."

                    wait = 5 * (attempt + 1) if err_type == "503" else 60
                    print(
                        f"[RETRY {attempt + 1}/{MAX_RETRIES}] {err_type} — {print_action}. Waiting {wait}s...",
                        file=sys.stderr, flush=True
                    )
                    time.sleep(wait)

            # Surface error to C# if all attempts failed
            if response is None:
                send_response({
                    "status":  "error",
                    "message": f"[API ERROR — {err_type}] {last_hint}"
                })

            # 2. Gọi AI sinh code (Dùng Langchain)
            final_json_str = clean_json_response(response.content)
            
            final_json_str = re.sub(r'\}\s*,\s*"global_tags"', r', "global_tags"', final_json_str)
            final_json_str = re.sub(r'\}\s*"global_tags"', r', "global_tags"', final_json_str)
            final_json_str = re.sub(r'\}\s*,\s*"global_timers"', r', "global_timers"', final_json_str)
            final_json_str = re.sub(r'\}\s*"global_timers"', r', "global_timers"', final_json_str)

            input_tokens = 0
            output_tokens = 0
            total_tokens = 0
            key_display = f"...{CURRENT_KEY[-10:]}" if 'CURRENT_KEY' in dir() else "USER_MODE"
            
            if hasattr(response, 'usage_metadata') and response.usage_metadata:
                usage = response.usage_metadata
                input_tokens = usage.get("input_tokens", 0)
                output_tokens = usage.get("output_tokens", 0)
                total_tokens = usage.get("total_tokens", 0)

            # 3. Bóc JSON ra, nhét token_count vào
            try:
                data_dict = json.loads(final_json_str)
                data_dict = normalize_body_code(data_dict)
                data_dict["input_tokens"] = input_tokens
                data_dict["output_tokens"] = output_tokens
                data_dict["token_usage"] = total_tokens
                data_dict["active_key"] = key_display
                data_dict["provider"] = provider_name
                final_json_str = json.dumps(data_dict, ensure_ascii=False, indent=2)
            except Exception as e:
                final_json_str = json.dumps({"error": f"Error loading Token: {str(e)}", "raw_output": final_json_str})

            # 4. Lưu lịch sử và in kết quả
            memory.save_turn(session_id, user_query, final_json_str)
            output_bytes = (final_json_str + "\n").encode('utf-8')
            sys.stdout.buffer.write(output_bytes)
            sys.stdout.buffer.flush()
            os._exit(0)   # bypass HuggingFace/ChromaDB atexit hangs

        except Exception as e:  
            error_res = {"status": "error", "message": str(e)}
            err_bytes = (json.dumps(error_res, ensure_ascii=False) + "\n").encode('utf-8')
            sys.stdout.buffer.write(err_bytes)
            sys.stdout.buffer.flush()
            os._exit(1)

if __name__ == "__main__":
    main()