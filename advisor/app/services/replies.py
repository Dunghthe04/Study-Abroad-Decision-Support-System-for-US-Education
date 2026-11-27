"""Câu cố định gửi cho học sinh, gom về một chỗ để rà chữ và sửa một lần.

Quy ước xưng hô (chốt theo số đông của dữ liệu huấn luyện): trợ lý xưng "mình", gọi học sinh là "bạn" (đúng như các câu hệ thống
mà model đọc trong dữ liệu). Chữ trên nút bấm là tin học sinh gửi đi nên viết theo giọng học sinh: "em" (39% câu hỏi trong dữ liệu
dùng "em", "mình" 2,7%, "tôi" 1,1%).

Chưa gom vào đây (làm ở bước sau): câu hỏi nhập hồ sơ QUESTIONS (chat_flow.py), lời nhắc nhập sai ở skill_ho_so.py và validators.py,
nhãn nút nhập hồ sơ ở buttons.py. Các mẫu có {chỗ trống} được điền bằng str.format ở nơi dùng.
"""

from app.schemas.chat import QuickReply

# ---------------------------------------------------------------------------------------------------------------------
# Nút dùng chung: mọi câu trả lời có thể rơi vào ngõ cụt đều kèm các nút này để học sinh luôn có đường đi tiếp
# ---------------------------------------------------------------------------------------------------------------------
MENU = {
    "schools": QuickReply(label="Gợi ý trường từ hồ sơ", value="Gợi ý trường phù hợp với hồ sơ của em"),
    "assess": QuickReply(label="Đánh giá hồ sơ", value="Đánh giá hồ sơ của em"),
    # Bấm nút này thì code trả lời luôn (chat_flow.is_try_menu), không qua SLM: câu chung chung "thử thay đổi" model chưa đọc được
    "scenario": QuickReply(label="Thử thay đổi hồ sơ", value="Em muốn thử thay đổi hồ sơ"),
    "knowledge": QuickReply(label="Hỏi về visa, học phí", value="Visa F-1 cần chuẩn bị gì?"),
    "centers": QuickReply(label="Tìm trung tâm tư vấn", value="Em muốn tìm trung tâm tư vấn"),
}


def menu_buttons(*skip: str) -> list[QuickReply]:
    """Bản sao danh sách nút dùng chung, bỏ các nút có tên trong skip (vd nút đang ở đúng việc đó)."""
    return [button.model_copy() for name, button in MENU.items() if name not in skip]


# ---------------------------------------------------------------------------------------------------------------------
# Ngoài phạm vi (branches.OutOfScopeBranch): theo subtype, không gọi model; mọi câu kèm menu_buttons()
# ---------------------------------------------------------------------------------------------------------------------
HANDOFF_AI = "Mình là trợ lý AI nên không thay được tư vấn viên."
# Muốn gặp người thật: trong hệ thống "người thật" là tư vấn viên của trung tâm, nên đi vào luồng trung tâm kèm câu này
HANDOFF_INTRO = f"{HANDOFF_AI} Mình gợi ý các trung tâm tư vấn đã xác minh."
# Hỏi về hệ thống (nguồn dữ liệu, ngày cập nhật, độ tin cậy): câu cố định kèm nút mở trang Căn cứ tham số (UC-157, UC-83)
ABOUT_SYSTEM = (
    "Mình là trợ lý AI tư vấn du học Mỹ. Mình đánh giá hồ sơ của bạn, gợi ý trường phù hợp, thử các thay đổi "
    "(như điểm SAT, GPA), giải đáp về visa, học phí, học bổng và giới thiệu trung tâm tư vấn đã xác minh. "
    "Số liệu về trường lấy từ cơ sở dữ liệu của hệ thống, kèm nguồn và ngày thu thập; kết quả gợi ý chỉ mang tính tham khảo. "
    "Căn cứ của các tham số tính gợi ý bạn xem ở trang Căn cứ tham số. "
    "Bạn muốn bắt đầu từ đâu?"
)
ABOUT_SYSTEM_PAGE_BUTTON = QuickReply(
    label="Xem căn cứ tham số", value="Em muốn xem căn cứ tham số của hệ thống", href="/methodology"
)
# Muốn gặp người thật mà gói không cho gợi ý trung tâm trong chat (Miễn phí): dẫn tới trang danh bạ Trung tâm (UC-40, UC-42).
# Không phải subtype của model: chat_flow đổi human_handoff thành loại này theo gói của học sinh
HANDOFF_FREE = "human_handoff_free"
HANDOFF_FREE_REPLY = f"{HANDOFF_AI} Bạn xem danh sách trung tâm tư vấn đã xác minh ở trang Trung tâm và gửi yêu cầu liên hệ nhé."
GREETING_REPLY = (
    "Xin chào! Mình là trợ lý tư vấn du học Mỹ. Mình có thể gợi ý trường phù hợp với hồ sơ của bạn, "
    "thử các kịch bản (nếu điểm SAT, GPA thay đổi), giải đáp về visa, học phí, học bổng, hoặc giới thiệu trung tâm tư vấn. "
    "Bạn muốn bắt đầu từ đâu?"
)
OUT_OF_SCOPE = {
    # Ngoài chủ đề, kể cả muốn học thạc sĩ hoặc tiến sĩ: hệ thống chỉ hỗ trợ du học Mỹ bậc đại học (UC-157, UC-08)
    "exception": "Hiện mình chỉ hỗ trợ tư vấn du học Mỹ bậc đại học, chưa hỗ trợ nước khác, thạc sĩ hay tiến sĩ. Bạn chọn một việc dưới đây nhé.",
    "sensitive": "Mình không hỗ trợ việc này. Hồ sơ du học cần trung thực, mình giúp bạn chuẩn bị đúng cách nhé.",
    # farewell gồm cảm ơn và tạm biệt nên dùng một câu hợp với cả hai
    "farewell": "Rất vui được hỗ trợ bạn! Khi cần bạn cứ quay lại hỏi mình nhé.",
    "ambiguous": "Mình chưa hiểu rõ ý bạn. Bạn chọn một việc dưới đây hoặc nói cụ thể hơn nhé.",
    "about_system": ABOUT_SYSTEM,
    # Chỉ dùng khi chưa có skill trung tâm; bình thường human_handoff đi vào luồng trung tâm (HANDOFF_INTRO)
    "human_handoff": f"{HANDOFF_AI} Bạn xem danh sách trung tâm tư vấn đã xác minh ở trang Trung tâm nhé.",
    HANDOFF_FREE: HANDOFF_FREE_REPLY,
}

# ---------------------------------------------------------------------------------------------------------------------
# Chưa làm được / chưa đủ điều kiện (chat_flow.py, branches.py)
# ---------------------------------------------------------------------------------------------------------------------
# Mọi ý của tin đều cần gói Premium (allowed_intents của .NET): trả câu này, không chạy nhánh nào
PREMIUM_REQUIRED = "Câu hỏi này cần tài khoản USDSS Premium. Bạn vẫn có thể hỏi kiến thức du học miễn phí."
NOT_READY = "Phần này mình đang hoàn thiện. Bạn chọn một việc khác dưới đây hoặc quay lại sau nhé."
NEED_PROFILE = (
    "Mình cần biết thêm về hồ sơ của bạn trước khi làm bước này. Bạn cho mình xin thêm thông tin nhé."
)
NEED_MAJOR = "Để gợi ý trường mình cần biết ngành bạn muốn học. Bạn chọn ngành giúp mình nhé."
BACK_TO_QUESTION = "Quay lại câu mình đang hỏi nhé: "
CANNOT_ANSWER = "Mình chưa viết được câu trả lời cho câu này. Bạn bấm hỏi lại giúp mình nhé."
ASK_AGAIN_LABEL = "Hỏi lại"
LOOKING_UP = "Mình đang tra số liệu của trường này nhé."
RECOMMENDING = "Mình lọc danh sách trường phù hợp với hồ sơ và ngành của bạn nhé."
# Tin "Đúng rồi" khi hồ sơ vừa đủ mà học sinh chưa hỏi câu tư vấn nào: đánh giá hồ sơ như trước đây
DEFAULT_QUESTION = "Đánh giá hồ sơ của em giúp em."
ASK_WHICH_SCHOOLS = "Bạn muốn mình giải thích trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé."
# UC-153 1a: hỏi cách tính điểm phù hợp mà không nêu trường. Câu cố định, không số liệu của trường nào và không con số trọng số (để khỏi lệch
# với bảng công bố); trọng số và ngưỡng cụ thể ở trang Căn cứ tham số (UC-83)
FIT_METHOD = (
    "Điểm phù hợp của một trường gồm năm phần: vừa tầm học thuật (hồ sơ của bạn so với sinh viên của trường), ngân sách, "
    "chất lượng trường (theo xếp hạng), học bổng cho du học sinh và tiếng Anh. Mỗi phần được chấm trên thang 100, nhân với "
    "trọng số của phần đó rồi cộng lại. Điểm này chỉ dùng để sắp xếp các trường trong cùng một nhóm, không làm trường đổi nhóm. "
    "Nếu bạn chỉnh các nút ưu tiên (chi phí thấp, học bổng, danh tiếng) thì trọng số đổi theo. "
    "Trọng số và các ngưỡng cụ thể được công bố ở trang Căn cứ tham số."
)
NEXT_AFTER_SAVE = "Bạn muốn mình làm gì tiếp?"
NEXT_AFTER_SAVE_REPLIES = [
    QuickReply(label="Gợi ý trường", value="Gợi ý trường cho em"),
    QuickReply(label="Đánh giá hồ sơ", value="Đánh giá hồ sơ của em"),
    QuickReply(label="Lên lộ trình", value="Lên lộ trình cho em"),
]

# ---------------------------------------------------------------------------------------------------------------------
# Kiến thức theo nhóm kết quả (branches.KnowledgeBranch, ý compare_facts có group)
# ---------------------------------------------------------------------------------------------------------------------
GROUP_NEED_RESULTS = "Bạn cần có danh sách trường gợi ý trước thì mình mới so sánh theo nhóm được. Bạn muốn mình gợi ý trường trước không?"
GROUP_TOO_SMALL = "Nhóm {group} trong danh sách của bạn có {count} trường, chưa đủ để so sánh. Bạn chọn nhóm khác hoặc nêu tên các trường nhé."

# ---------------------------------------------------------------------------------------------------------------------
# Kịch bản (branches.ScenarioBranch)
# ---------------------------------------------------------------------------------------------------------------------
NEED_RESULTS = "Bạn cần có danh sách trường gợi ý trước thì mình mới thử kịch bản được. Bạn muốn mình gợi ý trường trước không?"
NOTHING_TO_TRY = "Mình chưa thử được thay đổi này. Hiện mình thử được: {supported}."
TRY_INSTEAD = "Bạn thử đổi được: {supported}."
TRY_WHAT = (
    "Bạn muốn thử thay đổi gì? Mình thử được: {supported}. Bạn chọn nhanh bên dưới hoặc gõ mức bạn muốn thử."
)
ASK_SCHOOL = "Bạn muốn vào trường nào? Chọn hoặc gõ tên trường ở ô bên dưới nhé."
# Mục lọc trường: không thử tạm được, sửa hồ sơ rồi gợi ý lại (.NET không tự chạy lại gợi ý khi hồ sơ đổi)
FILTER_TEXT = (
    "{names} là điều kiện lọc trường nên mình không thử tạm được. Bạn sửa {names} trong hồ sơ rồi nhờ mình "
    "gợi ý trường lại thì danh sách sẽ theo {names} mới. Sau này muốn đổi lại lúc nào cũng được."
)
NO_EFFECT_TEXT = "{names} không làm thay đổi danh sách trường gợi ý."
# Câu giả định theo vòng nộp (UC-155): kết quả không đổi; nút này hỏi kiến thức về vòng nộp, trả lời từ Kho tri thức (UC-36)
ROUND_KNOWLEDGE_BUTTON = QuickReply(
    label="Tìm hiểu vòng nộp ED, EA, RD", value="ED, EA và RD khác nhau thế nào?"
)
CANNOT_TRY_OTHERS = "Mình chưa thử được: {names}."
EDIT_SECTION_LABEL = "Sửa {name}"
EDIT_SECTION_VALUE = "Em muốn sửa {name}"
TRIED_SCENARIO = "Mình thử kịch bản: {tried}.{note}"
GOAL_SEEK_WORKING = "Mình xem giúp bạn cần gì để vào {school} nhé."

# ---------------------------------------------------------------------------------------------------------------------
# Nhập hồ sơ trong chat hoặc form (intake.py, skills/profile_handler.py)
# ---------------------------------------------------------------------------------------------------------------------
INTAKE_ASK = "Để làm bước này mình cần hồ sơ của bạn. Bạn muốn trả lời ngay trong chat hay điền bằng form?"
INTAKE_ASK_MISSING = "Để làm bước này hồ sơ của bạn còn thiếu {missing} mục. Bạn muốn trả lời ngay trong chat hay điền bằng form?"
INTAKE_FORM = "Bạn điền hồ sơ ở form nhé, điền xong quay lại đây để mình làm tiếp."
INTAKE_MORE = "Hồ sơ của bạn còn thiếu một vài thông tin. "
# Nút "Tiếp tục bằng chat" trên form gửi đúng chữ này: hỏi tiếp trong chat các mục còn thiếu (không đổi chữ nếu chưa đổi form)
CONTINUE_CHAT = "Tiếp tục bằng chat"
FORM_DONE = "Hồ sơ của bạn đã đủ thông tin. Bạn muốn mình tư vấn gì tiếp?"
CHAT_BUTTON = QuickReply(label="Trả lời trong chat", value="Trả lời trong chat")
FORM_BUTTON = QuickReply(label="Điền bằng form", value="Điền bằng form")
# Tin khai hồ sơ nhưng không có gì mới để lưu (SLM cũng gắn loại này cho vài câu chỉ nói ý định): không khẳng định gì thêm,
# mời học sinh nói rõ nếu đang hỏi điều khác
PROFILE_DONE = (
    "Mình chưa thấy thông tin nào mới cần cập nhật vào hồ sơ của bạn. "
    "Nếu bạn đang muốn hỏi một điều khác, bạn nói rõ hơn giúp mình nhé."
)
# Kỳ nhập học đang chọn sớm hơn lúc tốt nghiệp THPT theo lớp đang học. Chỉ nhắc để kiểm tra, không chặn vì có em tốt nghiệp sớm
INTAKE_BEFORE_GRADUATION = (
    "Lưu ý: bạn dự kiến tốt nghiệp THPT năm {year}, nhưng kỳ nhập học đang chọn là {intake}, tức là trước khi tốt nghiệp. "
    "Thường học sinh nhập học sớm nhất vào Fall {year}, bạn kiểm tra lại giúp mình nhé."
)
# Học sinh hỏi hệ thống đang lưu gì (collect_profile, tasks status): đi kèm bảng hồ sơ
PROFILE_STATUS = 'Đây là hồ sơ mình đang lưu. Đúng thì bạn bấm "Đúng rồi", muốn đổi thì bấm "Cần sửa".'
PROFILE_NONE_YET = "Hiện mình chưa lưu thông tin hồ sơ nào của bạn."
# Ngân sách lưu theo khoảng như form, còn gợi ý trường tính tới trần của khoảng: phải nói cho học sinh biết
BUDGET_NOTE_NEW = "Bạn nói khoảng {said} USD mỗi năm. Mình lưu theo mức {label}."
BUDGET_NOTE_SAME = "{said} USD vẫn nằm trong mức {label} đang lưu nên hồ sơ không đổi."
BUDGET_NOTE_CEILING = (
    " Khi gợi ý trường, hệ thống tính theo mức {ceiling} USD và cho vượt thêm {percent}% (tới {limit} USD) "
    "nên có thể có trường tốn hơn mức bạn nói."
)
BUDGET_REVIEW_HINT = 'Bạn xem bảng hồ sơ: đúng thì bấm "Đúng rồi", muốn đổi thì bấm "Cần sửa".'
# Nút thử đúng số học sinh nói bằng thử giả định (không lưu vào hồ sơ). Câu gửi đi đã thử trên SLM v6: đúng 16/16
BUDGET_TRY_LABEL = "Thử với đúng {said} USD"
BUDGET_TRY_VALUE = "Nếu ngân sách của em là {said} USD một năm thì sao?"
# Còn thay đổi chưa xác nhận mà hỏi việc phải tính trên hồ sơ (UC-19, UC-154, UC-155): đi kèm bảng hồ sơ
UNSAVED_REVIEW = (
    'Bạn vừa đổi thông tin hồ sơ mà chưa xác nhận. Bạn xem bảng bên dưới: đúng thì bấm "Đúng rồi", '
    "mình lưu rồi trả lời câu bạn vừa hỏi."
)
# Học sinh sửa mà giá trị nói ra trùng giá trị đang lưu: đi kèm bảng hồ sơ
PROFILE_SAME = (
    "Thông tin bạn vừa nói trùng với hồ sơ đang lưu nên mình giữ nguyên. "
    'Bạn xem bảng hồ sơ: đúng thì bấm "Đúng rồi", muốn đổi thì bấm "Cần sửa".'
)
# Học sinh muốn làm lại hồ sơ (tasks restart): hỏi lại từng mục, giá trị cũ chỉ đổi khi học sinh trả lời giá trị mới
PROFILE_RESTART_INTRO = 'Mình hỏi lại từng mục từ đầu. Mục nào không đổi, bạn bấm "Giữ như cũ".'

# ---------------------------------------------------------------------------------------------------------------------
# Giải thích một trường (skills/skill_explain.py)
# ---------------------------------------------------------------------------------------------------------------------
EXPLAIN_WORKING = "Mình xem trường {name} giúp bạn nhé."
EXPLAIN_ASK_SCHOOL = "Bạn muốn mình giải thích trường nào trong danh sách?"
EXPLAIN_NO_LIST = (
    "Mình chưa có danh sách trường gợi ý nào để giải thích. Bạn cho mình gợi ý trường trước nhé."
)
EXPLAIN_BUTTON_VALUE = "Giải thích giúp em trường {name}"

# ---------------------------------------------------------------------------------------------------------------------
# Hỏi đáp kiến thức (skills/skill_tra_loi.py)
# ---------------------------------------------------------------------------------------------------------------------
# Kho chưa có câu trả lời: nói thẳng, không lan man. Đường đi tiếp (Tìm trung tâm tư vấn...) nằm ở nút menu_buttons("knowledge")
KNOWLEDGE_NO_BASIS = "Hiện hệ thống chưa có thông tin về vấn đề này."
KNOWLEDGE_UNAVAILABLE = "Phần trả lời kiến thức đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
KNOWLEDGE_DISCLAIMER = (
    "Thông tin chỉ mang tính tham khảo. Quy định visa, học phí và học bổng có thể thay đổi; "
    "hãy kiểm tra lại với nguồn chính thức hoặc trung tâm tư vấn."
)

# ---------------------------------------------------------------------------------------------------------------------
# Trung tâm (skills/skill_chuyen_trung_tam.py)
# ---------------------------------------------------------------------------------------------------------------------
CENTER_ASK_SERVICE = "Bạn cần hỗ trợ phần nào? Bạn chọn giúp mình nhé."
CENTER_INTRO = "Dưới đây là một số trung tâm đã xác minh có hỗ trợ {label}:"
CENTER_NONE = (
    "Hiện mình chưa có trung tâm đã xác minh nào hỗ trợ {label}. "
    "Bạn có thể xem danh sách trung tâm đầy đủ ở trang Trung tâm."
)
CENTER_UNAVAILABLE = "Phần gợi ý trung tâm đang tạm gián đoạn, bạn thử lại sau ít phút nhé."
# Học sinh nêu thành phố chưa có trung tâm nào hỗ trợ dịch vụ này: không đưa trung tâm ở nơi khác cho đủ, mà cho chọn nơi đang có
CENTER_NONE_IN_CITY = (
    "Tạm thời chưa có trung tâm đã xác minh nào ở {city} hỗ trợ {label}. Bạn chọn nơi khác đang có nhé:"
)
# Không nhận ra địa danh (vd "ĐN", "quận 1"): không được nói là ở đó chưa có trung tâm, vì chưa biết là đâu
CENTER_CITY_UNKNOWN = (
    "Mình chưa rõ “{city}” là tỉnh, thành nào. Dưới đây là những nơi đang có trung tâm hỗ trợ {label}, "
    "bạn chọn hoặc gõ vào ô tìm nhé:"
)
CENTER_ELSEWHERE = "Hiện chưa có trung tâm đã xác minh ở {city}, các trung tâm dưới đây ở nơi khác:"
CENTER_MORE = "Còn {count} trung tâm khác, bạn bấm Xem thêm để xem hết."
# Việc model tách ra (tasks): đặt lịch và so sánh. Hệ thống không có lịch hẹn riêng (UC-39 4a): dẫn tới form liên hệ UC-42 với lời nhắn điền sẵn.
# So sánh (UC-39 4b): chọn sẵn hai trung tâm đầu để mở bảng so sánh như UC-142. Cả hai vẫn kèm danh sách trung tâm theo dịch vụ
# Học sinh muốn đặt lịch mà chưa có danh sách trung tâm để bấm (chưa rõ dịch vụ, hoặc không có trung tâm): chỉ nói cách hẹn,
# không nhắc nút Liên hệ hay "đây là các trung tâm" vì chưa có gì hiện ra
CENTER_BOOKING_NOTE_NO_LIST = (
    "Hệ thống không có lịch hẹn riêng: bạn gửi yêu cầu liên hệ, trung tâm sẽ phản hồi để hẹn lịch với bạn."
)
CENTER_TASK_NOTES = {
    "booking": "Hệ thống không có lịch hẹn riêng: bạn gửi yêu cầu liên hệ, trung tâm sẽ phản hồi để hẹn lịch với bạn. Bạn bấm Liên hệ ở trung tâm muốn hẹn, lời nhắn đã điền sẵn dịch vụ và thành phố. Đây là các trung tâm đã xác minh.",
    "compare": "Mình chọn sẵn hai trung tâm đầu để bạn mở bảng so sánh (dịch vụ, bậc học, địa điểm); bạn có thể tick thêm hoặc bỏ chọn. Bảng chỉ nêu thông tin, mình không chấm hơn thua giữa các nơi. Đây là các trung tâm đã xác minh.",
}
# Lời nhắn điền sẵn trong form liên hệ khi học sinh muốn đặt lịch (UC-39 4a)
CENTER_BOOKING_NOTE = "Em muốn đặt lịch tư vấn về {label}"
CENTER_BOOKING_NOTE_IN_CITY = "Em muốn đặt lịch tư vấn về {label} ở {city}"
# Nút kèm câu "so sánh" và "đặt lịch": mở trang Trung tâm (bộ lọc, danh sách đầy đủ). value chỉ dùng khi nút không chuyển trang
CENTER_PAGE_BUTTON = QuickReply(
    label="Trang Trung tâm", value="Em muốn xem trang trung tâm tư vấn", href="/centers"
)
# Học sinh xin điều hệ thống không cung cấp (số điện thoại, giá, xếp hạng, đánh giá trung tâm)
CENTER_NOT_PROVIDED = (
    "Mình không cung cấp số điện thoại, mức giá hay xếp hạng, đánh giá của trung tâm. "
    "Mình chỉ gợi ý các trung tâm đã xác minh, bạn xem thông tin trên website của họ và tự hỏi trực tiếp nhé."
)
CENTER_DISCLAIMER = (
    "Danh sách chỉ gồm trung tâm đã được xác minh, xếp theo tên trung tâm, không phải xếp hạng chất lượng. "
    "Visa do viên chức lãnh sự quyết định, việc trúng tuyển do trường quyết định, nên không trung tâm nào bảo đảm được. "
    "Bạn nên tự tìm hiểu kỹ trước khi liên hệ."
)
CENTER_CITY_ASK = (
    "Cho em xem trung tâm {label} ở {city}"  # nút chọn thành phố: câu gửi lại, đã nêu rõ dịch vụ
)
CENTER_SERVICE_VALUE = "Em cần hỗ trợ {label}"  # nút chọn dịch vụ
# Khi bot đã biết thành phố, nút mang luôn thành phố để tin gửi đi tự đủ ý (không phụ thuộc model đoán là câu nối tiếp)
CENTER_SERVICE_VALUE_IN_CITY = "Em cần hỗ trợ {label} ở {city}"
