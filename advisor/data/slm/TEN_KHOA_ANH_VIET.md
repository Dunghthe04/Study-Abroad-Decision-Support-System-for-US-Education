# Hướng dẫn chuyển code và prompt sang tên tiếng Anh

Dữ liệu train đã **tiếng Anh**: `du_lieu_slm.xlsx` (nguồn) và `du_lieu_slm.jsonl` (file JSONL duy nhất, sinh bằng `python -m scripts.build_slm_jsonl`). Code, prompt và model v3 hiện vẫn dùng
**tên nội bộ tiếng Việt** ở cột phải các bảng bên dưới. File này nói rõ phải đổi gì thành gì. Chưa có hàm đổi tên tự động nào: code cũ **chưa đọc được** file JSONL mới cho tới khi anh em đổi.

## 1. Dạng một dòng của JSONL mới

```json
{"id": "collect_profile_001", "type": "collect_profile", "split": "train",
 "input": {"question": "em học lớp 10 ạ", "profile": null, "pending_question": "Hiện bạn đang học lớp mấy, hoặc đang làm gì?",
           "context": {"schools": ["Columbia"], "fields": ["cost"]}},
 "output": {"intents": [{"type": "collect_profile", "span": "em học lớp 10 ạ", "entities": {"current_grade": "em học lớp 10 ạ"}}]}}
```
`context` chỉ có khi có ngữ cảnh. `profile` là `null` khi chưa có hồ sơ. Đầu ra mong muốn của model là đúng khuôn `output` (`{"intents": [...]}`).

## 2. Việc cần đổi trong code (anh em tự làm)

| Nơi | Đổi gì |
|---|---|
| `app/schemas/slm.py` | Tên loại, khóa, mã nhiệm vụ, mã mục, bộ lọc, schema đầu ra SLM (`SLM_TIN_SCHEMA`): theo bảng bên dưới |
| `app/services/slm_classifier.py`, `slm_contract.py`, `conversation_context.py` | Đọc `intents`, `span`, `entities` thay cho `cac_y`, `doan`, `thuc_the`; dựng đầu vào với `question`, `profile`, `pending_question`, `context` |
| `app/services/branches.py`, `chat_flow.py`, `priority.py`… | Các nhánh theo loại ý và nhiệm vụ: đọc `tasks` (danh sách), `schools` (danh sách), `fields` (danh sách) |
| `app/skills/slm_intents.md` | **Đã có**: prompt tiếng Anh dùng để train (phiên bản 3.0). Lúc chạy phải dùng đúng prompt này. Prompt tiếng Việt `slm_phan_loai_cac_y*.md` giữ cho model v3, `slm_phan_loai_cac_y_ngan_v22.md` là bản đông lạnh của v3, **không sửa** |
| Tin nhắn gửi model lúc chạy | Dựng đúng như `user_message()` trong `scripts/export_slm_chat.py`: JSON viết liền `{"question","profile","pending_question"}`, thêm `context` khi có; hồ sơ là dict chữ, giá trị danh sách nối bằng `"; "` |
| `scripts/export_slm_chat.py` | **Đã đổi**: đọc `du_lieu_slm.jsonl` tiếng Anh, dùng prompt `slm_intents`, xuất `finetune/train.jsonl` và `val.jsonl` (hết train skill) |
| `scripts/eval_slm.py`, `eval_skill.py`, `slm_metrics.py`, `slm_charts.py`, `slm_report.py`, `make_ho_so_goc.py`, `fill_ho_so_excel.py` | Đọc `du_lieu_slm.jsonl` theo dạng mới (mục 1) và đầu ra model dạng `{"intents": [...]}` |
| `tests/` các file `test_slm_*`, `test_chat_*`, `test_priority.py`, `test_branches.py`… | Đổi tên theo bảng; `test_slm_data.py`, `test_slm_english.py`, `test_check_slm_data.py` đã dùng tên tiếng Anh |
| Train lại model | Bắt buộc: model v3 sinh tên tiếng Việt. Train bằng `du_lieu_slm.jsonl` với **đúng prompt mới** |

**Thứ tự gợi ý:** đổi schema và prompt, đổi code đọc đầu ra, đổi script đo và xuất train, đổi test, rồi train lại và đo lại. Backend .NET và frontend không dùng các tên này.

# Bảng đổi tên

Các bảng dưới đây đổi từng tên tiếng Anh (dữ liệu mới) sang tên nội bộ tiếng Việt (code hiện tại). Chúng sinh từ các bảng `EN_*` trong `scripts/build_slm_jsonl.py`, nguồn duy nhất của các cặp tên.

**Đây không chỉ là đổi tên.** Bản tiếng Anh có hình dạng đều hơn bản nội bộ ở vài chỗ, code phải đọc theo hình dạng mới:

- `tasks` luôn là danh sách. Bản nội bộ là `nhiem_vu` (một giá trị) cho `mac_dinh`, `kien_thuc`, `trung_tam`, và `cac_nhiem_vu` (danh sách) cho `ho_so`.
- `schools` luôn là danh sách ở `knowledge` và `profile_review` (một tên hoặc nhiều tên). Bản nội bộ tách thành `truong` (một tên) và `cac_truong` (từ hai tên, hoặc khi so sánh). `school` (một tên) chỉ dùng ở `scenario`.
- `fields` luôn là danh sách. Bản nội bộ là `muc`.
- `centers` (danh sách tên trung tâm) ↔ `cac_trung_tam`. `follow_up` ↔ `tiep_noi`. `context` ↔ `dang_noi_ve`. `target_group` nhận `reach` / `target` / `safety` (nội bộ: `Khó vào` / `Vừa sức` / `An toàn`).

## Loại ý và tên sheet

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `collect_profile` | `mac_dinh` |
| `profile_review` | `ho_so` |
| `scenario` | `kich_ban` |
| `knowledge` | `kien_thuc` |
| `center_referral` | `trung_tam` |
| `out_of_scope` | `ngoai_pham_vi` |

## Cột Excel và khóa đầu vào

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `question` | `cau_hoi` |
| `profile` | `ho_so` |
| `pending_question` | `cau_he_thong_dang_hoi` |
| `intents` | `cac_y` |
| `split` | `tap` |
| `context` | `dang_noi_ve` |

## Khóa của mỗi ý

Mỗi ý có đúng 3 khóa: `type`, `span`, `entities`.

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `span` | `doan` |
| `entities` | `thuc_the` |

## Khóa trong `entities`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `school` | `truong` |
| `city` | `thanh_pho` |
| `service` | `dich_vu` |
| `subtype` | `loai_con` |
| `mode` | `che_do` |
| `changes` | `thay_doi` |
| `factor` | `yeu_to` |
| `value` | `gia_tri` |
| `unit` | `don_vi` |
| `target_group` | `muc_tieu` |
| `action` | `hanh_dong` |

## Nhiệm vụ `collect_profile`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `answer` | `tra_loi` |
| `edit` | `sua` |
| `status` | `xem_ho_so` |
| `restart` | `lam_lai` |

## Nhiệm vụ `profile_review` (danh sách `tasks`)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `assessment` | `danh_gia` |
| `school_list` | `chon_truong` |
| `roadmap` | `lo_trinh` |
| `compare` | `so_sanh` |
| `explain_school` | `giai_thich_truong` |

## Nhiệm vụ `knowledge`

Câu hỏi kiến thức chung thì `tasks` để trống `[]`.

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `school_info` | `tra_cuu` |
| `compare_facts` | `so_sanh` |
| `search` | `tim_kiem` |

## Nhiệm vụ `center_referral`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `find` | `tim` |
| `booking` | `dat_lich` |
| `compare` | `so_sanh` |

## Mục thông tin của trường (`fields`)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `cost` | `chi_phi` |
| `aid` | `hoc_bong` |
| `english_requirement` | `tieng_anh` |
| `test_policy` | `diem_thi` |
| `gpa` | `gpa` |
| `acceptance_rate` | `ty_le_nhan` |
| `deadline` | `han_nop` |
| `graduation_rate` | `tot_nghiep` |
| `majors` | `nganh` |
| `ranking` | `xep_hang` |
| `location` | `vi_tri` |
| `website` | `website` |
| `housing` | `nha_o` |
| `student_faculty_ratio` | `ty_le_sv_giang_vien` |
| `student_activities` | `hoat_dong_sv` |
| `campus_setting` | `khuon_vien` |
| `other` | `khac` |

## Cách xếp (`sort`)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `ranking` | `xep_hang` |
| `lowest_cost` | `chi_phi_thap` |
| `easiest_admission` | `de_vao` |
| `most_selective` | `chon_loc` |

## Khóa bộ lọc (`filters`)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `state` | `bang` |
| `city` | `thanh_pho` |
| `major` | `nganh` |
| `school_type` | `loai_truong` |
| `aid` | `hoc_bong` |
| `test_policy` | `chinh_sach_thi` |
| `max_cost` | `chi_phi_toi_da` |
| `max_ielts` | `ielts_toi_da` |
| `max_toefl` | `toefl_toi_da` |
| `max_duolingo` | `duolingo_toi_da` |
| `min_acceptance_rate` | `ty_le_nhan_toi_thieu` |
| `max_acceptance_rate` | `ty_le_nhan_toi_da` |
| `max_rank` | `xep_hang_toi_da` |
| `min_graduation_rate` | `ty_le_tot_nghiep_toi_thieu` |
| `ivy` | `ivy` |

## Giá trị bộ lọc `loai_truong`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `public` | `cong_lap` |
| `private` | `tu_thuc` |

## Giá trị bộ lọc `hoc_bong`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `yes` | `co` |
| `need_based` | `theo_nhu_cau` |
| `merit_based` | `theo_thanh_tich` |

## Giá trị bộ lọc `chinh_sach_thi`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `optional` | `khong_bat_buoc` |
| `required` | `bat_buoc` |

## Giá trị bộ lọc `ivy`

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `yes` | `co` |

## Khóa ngữ cảnh (`context`)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `schools` | `truong` |
| `fields` | `muc` |
| `filters` | `bo_loc` |
| `scenario` | `kich_ban` |
| `last_field` | `muc_vua_nhap` |
| `last_task` | `viec_vua_lam` |
| `service` | `dich_vu` |
| `city` | `thanh_pho` |
| `centers` | `cac_trung_tam` |

## Giá trị có mã của `loai_con` (khóa nội bộ)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `exception` | `ngoai_le` |
| `sensitive` | `nhay_cam` |
| `farewell` | `tam_biet` |
| `ambiguous` | `mo_ho` |
| `about_system` | `ve_he_thong` |
| `human_handoff` | `gap_nguoi` |

## Giá trị có mã của `dich_vu` (khóa nội bộ)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `SCHOOL_SELECTION` | `CHON_TRUONG` |
| `ESSAY_EDITING` | `SUA_BAI_LUAN` |
| `APPLICATION_SUBMISSION` | `NOP_HO_SO` |
| `VISA_INTERVIEW_PREP` | `LUYEN_PHONG_VAN` |
| `TEST_PREP` | `LUYEN_THI` |
| `SCHOLARSHIP_SEARCH` | `SAN_HOC_BONG` |

## Giá trị có mã của `don_vi` (khóa nội bộ)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `scale_4` | `thang_4` |
| `scale_10` | `thang_10` |

## Giá trị có mã của `hanh_dong` (khóa nội bộ)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `skip` | `bo_qua` |

## Giá trị có mã của `muc_tieu` (khóa nội bộ)

| Tiếng Anh (dữ liệu mới, đích của code) | Tên nội bộ hiện tại (tiếng Việt) |
|---|---|
| `reach` | `Khó vào` |
| `target` | `Vừa sức` |
| `safety` | `An toàn` |
