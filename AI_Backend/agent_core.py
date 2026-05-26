# import json
# from langchain_core.tools import tool
# from langchain_google_genai import ChatGoogleGenerativeAI

# # ======================================================================
# # 1. ĐỊNH NGHĨA CÁC CÔNG CỤ (TOOLS) CHO AGENT
# # ======================================================================

# @tool
# def create_project_tool(project_name: str, folder_path: str):
#     """
#     Sử dụng công cụ này ĐỂ TẠO DỰ ÁN MỚI.
#     Lưu ý: Chỉ dùng công cụ này khi bạn được yêu cầu khởi tạo một dự án mới hoàn toàn. 
#     Không cần gọi hàm kết nối trước, hệ thống sẽ tự động xử lý kết nối ngầm.
#     - project_name: ...
#     - folder_path: ...
#     """
#     # Thay vì thực thi nội bộ, chúng ta đóng gói thành JSON để C# thi hành án

#     safe_path = folder_path.replace("\\", "/")

#     action_payload = {
#         "type": "agent_action",
#         "action": "CREATE_PROJECT",
#         "name": project_name,
#         "path": safe_path
#     }
#     return json.dumps(action_payload)

# @tool
# def open_project_tool(project_name: str, folder_path: str):
#     """
#     Sử dụng công cụ này KHI VÀ CHỈ KHI người dùng yêu cầu MỞ MỘT DỰ ÁN TIA PORTAL ĐÃ CÓ SẴN.
#     - project_name: Tên của dự án.
#     - folder_path: Đường dẫn đầy đủ tới tệp dự án (.ap19, .ap20,...) hoặc thư mục chứa dự án.
#     """
#     safe_path = folder_path.replace("\\", "/")

#     # Thay vì thực thi nội bộ, chúng ta đóng gói thành JSON để C# thi hành án
#     action_payload = {
#         "type": "agent_action",
#         "action": "OPEN_PROJECT",
#         "name": project_name,
#         "path": safe_path,
#     }
#     return json.dumps(action_payload)

# @tool
# def connect_tia_tool():
#     """
#     Sử dụng công cụ này KHI BẠN CẦN KẾT NỐI VỚI TIA PORTAL.
#     Công cụ này sẽ kiểm tra xem TIA Portal có đang mở không và thiết lập kết nối để thực hiện các thao tác tiếp theo.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "CONNECT_TIA",
#         "params": {}
#     })

# @tool
# def save_project_tool():
#     """
#     Sử dụng công cụ này sau khi hoàn thành các thay đổi quan trọng trong dự án TIA Portal
#     để đảm bảo dữ liệu được lưu lại an toàn.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "SAVE_PROJECT",
#         "params": {}
#     })

# @tool
# def close_tia_tool():
#     """
#     Sử dụng công cụ này khi muốn đóng dự án và giải phóng TIA Portal.
#     Lưu ý: Công cụ này sẽ đóng dự án và tắt tiến trình TIA Portal hoàn toàn.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "CLOSE_TIA",
#         "params": {}
#     })

# @tool
# def create_device_tool(device_name: str, ip_address: str, device_model_name: str):
#     """
#     Sử dụng công cụ này ĐỂ TẠO MỚI MỘT PLC HOẶC HMI/SCADA.
#     - device_name: Tên thiết bị. Nếu người dùng không nói rõ tên, hãy tự đặt mặc định là 'PC_Station_1' hoặc 'PLC_1'.
#     - ip_address: Địa chỉ IP. Nếu người dùng không cung cấp IP, hãy tự động điền mặc định là '192.168.0.1'.
#     - device_model_name: Tên thiết bị (ví dụ: 'Simatic WinCC Unified PC', 'S7-1200 1214C').
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "CREATE_DEVICE",
#         "name": device_name,
#         "ip": ip_address,
#         "model_name": device_model_name
#     })

# @tool
# def choose_device_tool(device_name: str):
#     """
#     Sử dụng công cụ này ĐỂ CHỌN THIẾT BỊ (PLC/SCADA) mà bạn muốn thực hiện các thao tác tiếp theo.
#     - device_name: Tên của thiết bị trong dự án (ví dụ: PLC_1, Pumping_PLC, My_HMI).
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "CHOOSE_DEVICE",
#         "name": device_name
#     })

# @tool
# def generate_code_tool(block_type: str, request_query: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI BẠN CẦN TẠO MÃ NGUỒN CHO TIA PORTAL.
#     - block_type: Loại block cần tạo, bao gồm: 
#       'ORGANIZATION_BLOCK' (OB), 'FUNCTION_BLOCK' (FB), 'FUNCTION' (FC), 
#       'DATA_BLOCK' (DB), 'HMI_SCREEN' (SCADA/HMI), 'CWC_SCREEN' (CWC).
#     - request_query: Mô tả chi tiết chức năng bạn muốn code thực hiện.
#     """
#     # Đóng gói để C# nhận diện và chạy hàm HandleChatAsync cũ
#     return json.dumps({
#         "type": "agent_action",
#         "action": "GENERATE_CODE",
#         "block_type": block_type,
#         "query": request_query
#     })

# @tool
# def import_fb_fc_tool(block_type: str, file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP FILE SCL CHO FUNCTION BLOCK (FB) HOẶC FUNCTION (FC).
#     - block_type: 'FB' hoặc 'FC'.
#     - file_names: Tên một hoặc nhiều file (ví dụ: 'Pump.scl, Motor.scl').
#       Hệ thống sẽ tìm trong thư mục Generated_Files.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "IMPORT_FB_FC",
#         "block_type": block_type,
#         "file_names": file_names
#     })

# @tool
# def import_ob_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP CODE LOGIC CỦA OB (ORGANIZATION BLOCK).
#     NỘI DUNG LÀ FILE .SCL, KHÔNG PHẢI LÀ FILE CSV CỦA TAGS.
#     - file_names: Tên một hoặc nhiều file (ví dụ: 'OB_Main_process.scl, Main_process.scl').
#       Hệ thống sẽ tìm trong thư mục Generated_Files.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "IMPORT_OB",
#         "file_names": file_names
#     })

# @tool
# def import_plc_tags_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP DANH SÁCH BIẾN (TAG TABLE/CSV) VÀO PLC.
#     NỘI DUNG LÀ FILE .CSV, KHÔNG PHẢI LÀ FILE SCL CỦA CODE BLOCK.
#     - file_names: Tên một hoặc nhiều file (ví dụ: 'Tags.csv, PLC_Tags.csv').
#       Hệ thống sẽ tìm trong thư mục Generated_Files.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "IMPORT_PLC_TAGS",
#         "file_names": file_names
#     })

# @tool
# def draw_scada_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI CẦN TỰ ĐỘNG DỰNG HOẶC VẼ MÀN HÌNH SCADA/HMI TỪ FILE CẤU TRÚC JSON.
#     NỘI DUNG BẮT BUỘC LÀ FILE ĐUÔI .JSON (Ví dụ: 'scada_layout.json', 'Screen_Main.json').
#     - file_names: Tên một hoặc nhiều file (ví dụ: 'scada_layout.json, Screen_Main.json').
#       Hệ thống sẽ tìm trong thư mục Generated_Files.
#     """
#     return json.dumps({
#         "type": "agent_action",
#         "action": "DRAW_SCADA",
#         "file_names": file_names
#     })

# # List of tools for agent to use
# AGENT_TOOLS = [create_project_tool, 
#                open_project_tool, 
#                connect_tia_tool, 
#                save_project_tool, 
#                close_tia_tool, 
#                create_device_tool, 
#                choose_device_tool, 
#                generate_code_tool, 
#                import_fb_fc_tool, 
#                import_ob_tool,
#                import_plc_tags_tool,
#                draw_scada_tool]

# # ======================================================================
# # 2. HÀM XỬ LÝ CHÍNH ĐƯỢC GỌI TỪ MAIN.PY
# # ======================================================================

# def process_agent_query(user_query: str, api_key: str):
#     debug_logs = []
#     try:
#         llm = ChatGoogleGenerativeAI(
#             model="gemini-3.5-flash",
#             temperature=0.0,
#             google_api_key=api_key
#         )
        
#         system_instruction = (
#             "Bạn là trợ lý AI điều phối TIA Portal chuyên nghiệp, có nhiệm vụ chuyển đổi yêu cầu "
#             "của người dùng thành một chuỗi (mảng) các hành động công cụ (tool calls) hợp lý.\n\n"
            
#             "QUY TẮC ƯU TIÊN VÀ LUỒNG THỰC THI BẮT BUỘC:\n"
#             "1. ĐIỀU KIỆN KẾT NỐI (TIÊN QUYẾT):\n"
#             "   - Khi người dùng yêu cầu bất kỳ tính năng cấu hình phần cứng hoặc phần mềm nào "
#             "     (như: tạo thiết bị, nạp biến tags, nạp khối hàm code, dựng màn hình SCADA...),\n"
#             "     BẮT BUỘC phải xếp 'connect_tia_tool' lên đầu tiên trong chuỗi hành động để hệ thống khởi chạy kết nối trước.\n\n"
            
#             "2. ĐIỀU KIỆN QUẢN LÝ DỰ ÁN (PROJECT CONTEXT):\n"
#             "   - CHỈ gọi 'create_project_tool' hoặc 'open_project_tool' khi người dùng nói rõ ràng "
#             "     các từ khóa như 'tạo dự án mới', 'mở dự án có sẵn' kèm theo đường dẫn tệp cụ thể.\n"
#             "   - Tuyệt đối KHÔNG tự ý tạo hoặc mở dự án mới nếu người dùng chỉ ra lệnh cấu hình thiết bị/logic.\n\n"
            
#             "3. ĐIỀU KIỆN RÀNG BUỘC NGỮ CẢNH THIẾT BỊ (DEVICE CONTEXT):\n"
#             "   - Để thực hiện các hành động: nạp code (import_fb_fc_tool, import_ob_tool), nạp biến (import_plc_tags_tool), "
#             "     hoặc dựng giao diện (draw_scada_tool), hệ thống bắt buộc phải biết mục tiêu hành động nằm ở thiết bị nào.\n"
#             "   - Do đó, nếu trong câu lệnh xuất hiện tên thiết bị (ví dụ: PLC_1, WinCC_PC...) hoặc có hành động import/draw, "
#             "     bạn BẮT BUỘC phải xếp 'choose_device_tool' ngay TRƯỚC các hành động nạp/vẽ đó.\n\n"
            
#             "4. LUỒNG TƯ DUY MẪU (PIPELINE SEQUENCE):\n"
#             "   - Yêu cầu: 'connect với project rồi nạp file biến OB30_Tags.csv vào PLC_1'\n"
#             "   - Chuỗi Tool trả về phải đúng thứ tự: [connect_tia_tool -> choose_device_tool(device_name='PLC_1') -> import_plc_tags_tool(file_names='OB30_Tags')]\n\n"
            
#             "5. PHÂN BIỆT ĐUÔI FILE KHI IMPORT:\n"
#             "   - File '.csv' hoặc liên quan từ khóa 'Tags' -> Dùng 'import_plc_tags_tool'.\n"
#             "   - File '.json' hoặc liên quan từ khóa 'draw', 'vẽ', 'màn hình' -> Dùng 'draw_scada_tool'.\n"
#             "   - File '.scl' hoặc liên quan từ khóa 'code', 'logic', 'khối hàm' -> Dùng 'import_ob_tool' (cho OB) hoặc 'import_fb_fc_tool' (cho FB/FC)."

#             "6. QUY TẮC BÙ THAM SỐ THIẾU:\n"
#             "   - Nếu người dùng yêu cầu tạo thiết bị mà không nói rõ IP hoặc Tên, hãy tự động điền mặc định '192.168.0.1' và 'Device_1'.\n"
#             "   - Nếu người dùng yêu cầu import/draw màn hình mà thiếu đuôi file, hãy tự động thêm đuôi '.json' (cho màn hình) hoặc '.scl' (cho khối hàm) vào tham số."
#         )
        
#         llm_with_tools = llm.bind_tools(AGENT_TOOLS)
        
#         # Kết hợp system instruction
#         full_prompt = f"{system_instruction}\n\nNgười dùng yêu cầu: {user_query}"
#         # full_prompt = f"Người dùng yêu cầu: {user_query}"
#         response = llm_with_tools.invoke(full_prompt)
#         # 🌟 PHÂN LUỒNG NHIỀU TOOL CÙNG LÚC
#         if response.tool_calls:
#             results = []
            
#             # Duyệt qua tất cả các tool mà AI muốn gọi
#             for tool_call in response.tool_calls:
#                 tool_name = tool_call['name']
#                 tool_args = tool_call['args']
                
#                 tool_executed = False
#                 for tool_obj in AGENT_TOOLS:
#                     if tool_obj.name == tool_name:
#                         # Thực thi tool và nhận kết quả JSON
#                         result_json = tool_obj.invoke(tool_args)
#                         results.append(json.loads(result_json))
#                         tool_executed = True
#                         break
                
#                 if not tool_executed:
#                     return json.dumps({"status": "error", "message": f"Tool {tool_name} không tồn tại"})
            
#             # Trả về một list các action cho C# xử lý theo hàng đợi
#             return json.dumps({
#                 "type": "multi_action", 
#                 "actions": results
#             })

#         else:
#             return json.dumps({
#                 "type": "chat_response",
#                 "content": response.content
#             })

#     except Exception as e:
#         return json.dumps({
#             "status": "error",
#             "message": f"Agent Runtime Error: {str(e)}"
#         })

# import json
# from langchain_core.tools import tool
# from langchain_google_genai import ChatGoogleGenerativeAI

# # ======================================================================
# # 1. ĐỊNH NGHĨA CÁC CÔNG CỤ (CHỈ TRẢ VỀ PAYLOAD THÔ, KHÔNG BỌC TYPE ĐƠN)
# # ======================================================================

# @tool
# def create_project_tool(project_name: str, folder_path: str):
#     """
#     Sử dụng công cụ này ĐỂ TẠO DỰ ÁN MỚI.
#     Lưu ý: Chỉ dùng khi được yêu cầu khởi tạo một dự án mới hoàn toàn.
#     """
#     safe_path = folder_path.replace("\\", "/")
#     return json.dumps({
#         "action": "CREATE_PROJECT",
#         "name": project_name,
#         "path": safe_path
#     })

# @tool
# def open_project_tool(project_name: str, folder_path: str):
#     """
#     Sử dụng công cụ này KHI VÀ CHỈ KHI người dùng yêu cầu MỞ MỘT DỰ ÁN TIA PORTAL ĐÃ CÓ SẴN.
#     """
#     safe_path = folder_path.replace("\\", "/")
#     return json.dumps({
#         "action": "OPEN_PROJECT",
#         "name": project_name,
#         "path": safe_path
#     })

# @tool
# def connect_tia_tool():
#     """
#     Sử dụng công cụ này KHI BẠN CẦN KẾT NỐI VỚI TIA PORTAL.
#     """
#     return json.dumps({
#         "action": "CONNECT_TIA"
#     })

# @tool
# def save_project_tool():
#     """
#     Sử dụng công cụ này để lưu dự án TIA Portal.
#     """
#     return json.dumps({
#         "action": "SAVE_PROJECT"
#     })

# @tool
# def close_tia_tool():
#     """
#     Sử dụng công cụ này khi muốn đóng dự án và tắt TIA Portal.
#     """
#     return json.dumps({
#         "action": "CLOSE_TIA"
#     })

# @tool
# def create_device_tool(device_name: str, ip_address: str, device_model_name: str):
#     """
#     Sử dụng công cụ này ĐỂ TẠO MỚI MỘT PLC HOẶC HMI/SCADA.
#     - device_name: Tên thiết bị. Nếu thiếu, đặt mặc định là 'PC_Station_1' hoặc 'PLC_1'.
#     - ip_address: Địa chỉ IP. Nếu thiếu, đặt mặc định là '192.168.0.1'.
#     - device_model_name: Tên dòng máy (ví dụ: 'Simatic WinCC Unified PC', 'S7-1200 1214C').
#     """
#     return json.dumps({
#         "action": "CREATE_DEVICE",
#         "name": device_name,
#         "ip": ip_address,
#         "model_name": device_model_name
#     })

# @tool
# def choose_device_tool(device_name: str):
#     """
#     Sử dụng công cụ này ĐỂ KHÓA/CHỌN THIẾT BỊ ĐÍCH (PLC hoặc HMI/SCADA PC Station) 
#     trước khi tiến hành nạp khối hàm (SCL), nạp bảng biến (CSV), hoặc dựng giao diện (JSON).
#     - device_name: Tên của thiết bị xuất hiện trong câu lệnh (ví dụ: 'PLC_1', 'Syrup_scada', 'WinCC_PC').
#     """
#     return json.dumps({
#         "action": "CHOOSE_DEVICE",
#         "name": device_name
#     })

# @tool
# def generate_code_tool(block_type: str, request_query: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI BẠN CẦN TẠO MÃ NGUỒN CHO TIA PORTAL.
#     """
#     return json.dumps({
#         "action": "GENERATE_CODE",
#         "block_type": block_type,
#         "query": request_query
#     })

# @tool
# def import_fb_fc_tool(block_type: str, file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP FILE SCL CHO FUNCTION BLOCK (FB) HOẶC FUNCTION (FC).
#     """
#     return json.dumps({
#         "action": "IMPORT_FB_FC",
#         "block_type": block_type,
#         "file_names": file_names
#     })

# @tool
# def import_ob_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP CODE LOGIC CỦA OB (ORGANIZATION BLOCK).
#     """
#     return json.dumps({
#         "action": "IMPORT_OB",
#         "file_names": file_names
#     })

# @tool
# def import_plc_tags_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP DANH SÁCH BIẾN (TAG TABLE/CSV) VÀO PLC.
#     """
#     return json.dumps({
#         "action": "IMPORT_PLC_TAGS",
#         "file_names": file_names
#     })

# @tool
# def draw_scada_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI CẦN TỰ ĐỘNG DỰNG HOẶC VẼ MÀN HÌNH SCADA/HMI TỪ FILE CẤU TRÚC JSON.
#     """
#     return json.dumps({
#         "action": "DRAW_SCADA",
#         "file_names": file_names
#     })

# @tool
# def import_hmi_tags_tool(file_names: str):
#     """
#     DÙNG CÔNG CỤ NÀY KHI NẠP DANH SÁCH BIẾN ĐỊA CHỈ (TAG TABLE/CSV) VÀO TRẠM HMI HOẶC SCADA RUNTIME.
#     NỘI DUNG BẮT BUỘC LÀ FILE ĐUÔI .CSV (Ví dụ: 'hmi_tags.csv', 'SCADA_Variables.csv').
#     Hệ thống sẽ tự động tìm kiếm các tệp này trong thư mục Generated_Files.
#     - file_names: Tên một hoặc nhiều file biến CSV của HMI, cách nhau bởi dấu phẩy.
#     """
#     return json.dumps({
#         "action": "IMPORT_HMI_TAGS",
#         "file_names": file_names
#     })

# @tool
# def create_hmi_connection_tool(communication_driver: str = "SIMATIC S7 1200/1500", hmi_ip: str = "192.168.0.2", plc_ip: str = "192.168.0.1", access_point: str = "S7ONLINE"):
#     """
#     DÙNG CÔNG CỤ NÀY ĐỂ TẠO DÒNG KẾT NỐI TRUYỀN THÔNG (CONNECTION) GIỮA TRẠM HMI/SCADA VÀ PLC.
#     BẮT BUỘC phải gọi công cụ này TRƯỚC KHI thực hiện nạp biến HMI Tags (import_hmi_tags_tool).
#     - communication_driver: Trình điều khiển truyền thông (Ví dụ: 'SIMATIC S7 1200/1500', 'Modbus TCP'). Mặc định: 'SIMATIC S7 1200/1500'.
#     - hmi_ip: Địa chỉ IP cấu hình cho trạm HMI/SCADA. Mặc định: '192.168.0.2'.
#     - plc_ip: Địa chỉ IP của đối tác PLC kết nối. Mặc định: '192.168.0.1'.
#     - access_point: Điểm truy cập mạng của HMI. Mặc định: 'S7ONLINE'.
#     """
#     return json.dumps({
#         "action": "CREATE_HMI_CONNECTION",
#         "driver": communication_driver,
#         "hmi_ip": hmi_ip,
#         "plc_ip": plc_ip,
#         "access_point": access_point
#     })

# @tool
# def execute_pipeline_tool(actions_list: str):
#     """
#     BẮT BUỘC SỬ DỤNG CÔNG CỤ NÀY KHI NGƯỜI DÙNG YÊU CẦU THỰC HIỆN CHUỖI HÀNH ĐỘNG TRÊN TIA PORTAL.
#     - actions_list: Chuỗi JSON định dạng mảng chứa các hành động cần thực hiện tuần tự.
#       Ví dụ: '[{"action": "CONNECT_TIA"}, {"action": "CHOOSE_DEVICE", "name": "Wincc_PC_1"}, {"action": "CREATE_HMI_CONNECTION", "hmi_ip": "192.168.0.90", "plc_ip": "192.168.0.10"}]'
#     """
#     return json.dumps({
#         "type": "multi_action",
#         "actions": json.loads(actions_list)
#     })

# AGENT_TOOLS = [execute_pipeline_tool]

# # ======================================================================
# # 2. HÀM XỬ LÝ ĐIỀU PHỐI CHÍNH
# # ======================================================================

# def process_agent_query(user_query: str, api_key: str):
#     debug_logs = []
#     try:
#         llm = ChatGoogleGenerativeAI(
#             model="gemini-3.5-flash",
#             temperature=0.0,
#             google_api_key=api_key
#         )
        
#         system_instruction = (
#             "Bạn là chuyên gia điều phối kịch bản tự động hóa TIA Portal.\n"
#             "Nhiệm vụ của bạn là phân tích câu lệnh người dùng và dịch nó thành một chuỗi mảng JSON "
#             "để truyền vào công cụ 'execute_pipeline_tool'.\n\n"
#             "CÁC HÀNH ĐỘNG BẠN CÓ THỂ ĐƯA VÀO MẢNG JSON:\n"
#             "- {'action': 'CONNECT_TIA'}: Luôn đặt lên đầu tiên nếu câu lệnh yêu cầu cấu hình/nạp.\n"
#             "- {'action': 'CHOOSE_DEVICE', 'name': 'Tên_Thiết_Bị'}: Chọn thiết bị đích trước khi import/create connection.\n"
#             "- {'action': 'CREATE_HMI_CONNECTION', 'driver': '...', 'hmi_ip': '...', 'plc_ip': '...', 'access_point': '...'}: Tạo kết nối SCADA-PLC.\n"
#             "- {'action': 'IMPORT_HMI_TAGS', 'file_names': '...'}: Nạp biến HMI.\n\n"
#             "QUY TẮC: Trích xuất chính xác địa chỉ IP trong dấu ngoặc đơn của trạm WinCC và PLC để điền vào tham số."
#         )
        
#         llm_with_tools = llm.bind_tools(AGENT_TOOLS)
#         full_prompt = f"{system_instruction}\n\nNgười dùng yêu cầu: {user_query}"
#         response = llm_with_tools.invoke(full_prompt)
        
#         if response.tool_calls:
#             # Lấy kết quả đóng gói trực tiếp từ Super Tool
#             tool_call = response.tool_calls[0]
#             raw_result = execute_pipeline_tool.invoke(tool_call['args'])
            
#             # Trả thẳng JSON hoàn chỉnh về cho C# xử lý
#             return raw_result
#         else:
#             return json.dumps({
#                 "type": "chat_response",
#                 "content": response.content
#             })
            
#     except Exception as e:
#         return json.dumps({"status": "error", "message": str(e)})


import json
import sys
from langchain_core.tools import tool
from langchain_google_genai import ChatGoogleGenerativeAI

# ======================================================================
# 1. SUPER TOOL — handles both str and list from Gemini
# ======================================================================

@tool
def execute_pipeline_tool(actions_list: str):
    """
    BẮT BUỘC SỬ DỤNG CÔNG CỤ NÀY KHI NGƯỜI DÙNG YÊU CẦU THỰC HIỆN BẤT KỲ HÀNH ĐỘNG 
    HOẶC CHUỖI HÀNH ĐỘNG NÀO TRÊN TIA PORTAL.
    - actions_list: Chuỗi JSON định dạng mảng chứa các hành động cần thực hiện tuần tự.
    """
    # Gemini sometimes passes a Python list directly instead of a JSON string.
    # Handle both cases defensively.
    if isinstance(actions_list, list):
        actions = actions_list
    elif isinstance(actions_list, str):
        actions = json.loads(actions_list)
    else:
        actions = []

    return json.dumps({
        "type": "multi_action",
        "actions": actions
    }, ensure_ascii=False)

AGENT_TOOLS = [execute_pipeline_tool]

# ======================================================================
# 2. MAIN DISPATCHER
# ======================================================================

def process_agent_query(user_query: str, api_key: str):
    try:
        llm = ChatGoogleGenerativeAI(
            model="gemini-3.5-flash",
            temperature=0.0,
            google_api_key=api_key
        )

        system_instruction = (
            "Bạn là trợ lý AI điều phối kịch bản tự động hóa TIA Portal v20.\n"
            "Nhiệm vụ của bạn là phân tích câu lệnh người dùng và dịch nó thành một chuỗi mảng JSON "
            "chứa các hành động tuần tự, rồi truyền vào công cụ 'execute_pipeline_tool'.\n\n"

            "DANH SÁCH CÁC HÀNH ĐỘNG HỢP LỆ ĐỂ ĐƯA VÀO MẢNG JSON:\n"
            "- {'action': 'CONNECT_TIA'}: Kết nối với phần mềm TIA Portal. Luôn đặt lên đầu tiên nếu lệnh yêu cầu cấu hình/nạp phần cứng/phần mềm.\n"
            "- {'action': 'CREATE_PROJECT', 'name': '...', 'path': '...'}: Khởi tạo dự án mới hoàn toàn.\n"
            "- {'action': 'OPEN_PROJECT', 'name': '...', 'path': '...'}: Mở dự án đã có sẵn.\n"
            "- {'action': 'SAVE_PROJECT'}: Lưu dự án hiện tại.\n"
            "- {'action': 'CLOSE_TIA'}: Đóng và tắt tiến trình TIA Portal.\n"
            "- {'action': 'CREATE_DEVICE', 'name': '...', 'ip': '...', 'model_name': '...', 'version': '...'}: Tạo mới PLC hoặc HMI.\n"
            "  + model_name: Tên hoặc mã OrderNumber của thiết bị (Ví dụ: '6ES7 513-1AL00-0AB0').\n"
            "  + version: Phiên bản firmware mong muốn (Ví dụ: 'V2.2', 'V3.0'). Nếu người dùng không nói, hãy để trống hoặc trả về null.\n"                                                                                                                                                                                                  
            "  + model_name: Cần gọi chính xác như trong catalog: 'Simatic WinCC Unified PC', 'MTP700 Unified Comfort Panel', hoặc các dòng PLC 'S7-1200 CPU...'.\n"
            "- {'action': 'CHOOSE_DEVICE', 'name': '...'}: Chọn thiết bị/trạm đích hiện hành để thao tác. Bắt buộc phải có trước khi thực hiện nạp hoặc đổi IP.\n"
            "- {'action': 'IMPORT_FB_FC', 'block_type': 'FB hoặc FC', 'file_names': '...'}: Nạp file SCL cho khối hàm FB/FC.\n"
            "- {'action': 'IMPORT_OB', 'file_names': '...'}: Nạp file SCL cho khối chức năng hệ thống OB.\n"
            "- {'action': 'IMPORT_PLC_TAGS', 'file_names': '...'}: Nạp bảng biến CSV vào PLC.\n"
            "- {'action': 'IMPORT_HMI_TAGS', 'file_names': '...'}: Nạp bảng biến CSV vào trạm WinCC Unified/HMI.\n"
            "- {'action': 'CREATE_HMI_CONNECTION', 'driver': '...', 'hmi_ip': '...', 'plc_ip': '...', 'access_point': '...'}: Tạo kết nối truyền thông HMI-PLC. Luôn đặt trước import_hmi_tags_tool.\n"
            "  + driver: BẮT BUỘC chọn 1 trong các giá trị sau: 'SIMATIC S7 1200/1500', 'Modbus TCP', 'PROFINET', 'OPC UA'.\n"
            "  + KHÔNG ĐƯỢC tự ý viết tắt hoặc viết sai tên driver."
            "- {'action': 'DRAW_SCADA', 'file_names': '...'}: Dựng màn hình SCADA/HMI từ file JSON.\n"
            "- {'action': 'CHANGE_IP', 'ip': '...', 'subnet': '...', 'gateway': '...'}: Thay đổi thông số IP mạng của thiết bị.\n"
            "- {'action': 'COMPILE', 'mode': '...', 'rebuild': bool}: Biên dịch thiết bị đang chọn.\n"
            "  + 'mode': 'hw', 'sw', hoặc 'both'. Mặc định là 'both'.\n"
            "  + 'rebuild': true/false. Mặc định là false.\n"
            "- {'action': 'ADD_MODULE', 'model_name': '...', 'slot': int}: Gắn thêm module mở rộng vào Rack.\n"
            "- {'action': 'GENERATE_CODE', 'block_type': '...', 'query': '...'}: Kích hoạt mô hình AI sinh mã nguồn.\n"
            "  + 'block_type': 'ORGANIZATION_BLOCK', 'FUNCTION_BLOCK', 'FUNCTION', 'DATA_BLOCK', 'HMI_SCREEN', 'CWC_SCREEN'.\n\n"

            "QUY TẮC:\n"
            "1. Khi nạp/đổi IP cho thiết bị cụ thể → tự động chèn CHOOSE_DEVICE trước.\n"
            "2. Khi COMPILE hoặc ADD_MODULE cho thiết bị cụ thể → tự động chèn CHOOSE_DEVICE trước.\n"
            "3. Đuôi .csv của PLC → IMPORT_PLC_TAGS | Đuôi .csv của HMI → IMPORT_HMI_TAGS.\n"
            "4. Đuôi .json → DRAW_SCADA | Đuôi .scl → IMPORT_OB (nếu tên chứa OB) hoặc IMPORT_FB_FC.\n"
            "5. Nếu thiếu Subnet Mask → mặc định '255.255.255.0'. Nếu thiếu Gateway → mặc định ''.\n\n"

            "VÍ DỤ TẠO HMI:\n"
            "Yêu cầu: 'create wincc system named HMI_1'\n"
            "JSON: [{\"action\": \"CONNECT_TIA\"}, {\"action\": \"CREATE_DEVICE\", \"name\": \"HMI_1\", \"ip\": \"192.168.0.2\", \"model_name\": \"Simatic WinCC Unified PC\"}]\n"

            "VÍ DỤ:\n"
            "Yêu cầu: 'connect and compile software for PLC_1'\n"
            "JSON: [{\"action\": \"CONNECT_TIA\"}, {\"action\": \"CHOOSE_DEVICE\", \"name\": \"PLC_1\"}, {\"action\": \"COMPILE\", \"mode\": \"sw\", \"rebuild\": false}]\n"
        
            "VÍ DỤ (TẠO THIẾT BỊ VỚI PHIÊN BẢN CỤ THỂ):\n"
            "Yêu cầu: 'create a cpu 1212C DC/DC/Rly with version 4.5'\n"
            "JSON: [{\"action\": \"CONNECT_TIA\"}, {\"action\": \"CREATE_DEVICE\", \"name\": \"PLC_1\", \"ip\": \"192.168.0.1\", \"model_name\": \"S7-1200 CPU 1212C DC/DC/Rly\", \"version\": \"V4.5\"}]\n\n"

            "VÍ DỤ (TẠO THIẾT BỊ TỰ ĐỘNG CHỌN PHIÊN BẢN MỚI NHẤT):\n"
            "Yêu cầu: 'create a cpu 1212C DC/DC/Rly for PLC_2'\n"
            "JSON: [{\"action\": \"CONNECT_TIA\"}, {\"action\": \"CREATE_DEVICE\", \"name\": \"PLC_2\", \"ip\": \"192.168.0.2\", \"model_name\": \"S7-1200 CPU 1212C DC/DC/Rly\"}]"
        )

        llm_with_tools = llm.bind_tools(AGENT_TOOLS)
        full_prompt = f"{system_instruction}\n\nNgười dùng yêu cầu: {user_query}"
        response = llm_with_tools.invoke(full_prompt)

        # Handle tool calls
        if response.tool_calls:
            tool_call = response.tool_calls[0]
            raw_result = execute_pipeline_tool.invoke(tool_call['args'])
            return raw_result

        # Handle plain text response (no tool call fired)
        # response.content may be a string or a list of content blocks (newer Gemini)
        content = response.content
        if isinstance(content, list):
            content = " ".join(
                block.get("text", "") if isinstance(block, dict) else str(block)
                for block in content
            ).strip()

        return json.dumps({
            "type": "chat_response",
            "content": content or "Agent did not produce a response."
        }, ensure_ascii=False)

    except Exception as e:
        return json.dumps({"status": "error", "message": f"Agent Error: {str(e)}"}, ensure_ascii=False)
            
    except Exception as e:
        return json.dumps({"status": "error", "message": f"Agent Error: {str(e)}"})