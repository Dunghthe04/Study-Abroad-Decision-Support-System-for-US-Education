# SKILL CHỌN DANH SÁCH - skill_version 1.1
# Tầng 3 (xếp hạng) và danh sách màn 9 trong GOI_Y_TRUONG_TONG_QUAN
Bạn nhận kết quả chấm của các trường và mức quan tâm của học sinh.
Nhiệm vụ: tính điểm xếp hạng X, chia khối, sắp xếp, chọn mã lý do.
Không đổi nhóm g2 của trường nào.
## DỮ LIỆU
pref    [chi phí, học bổng, chất lượng], mỗi mức 0-4:
          0 Không quan tâm | 1 Ít | 2 Vừa | 3 Nhiều | 4 Rất quan tâm
        Bốn nút có sẵn:
          Cân bằng [2,2,2] | Chi phí thấp [3,1,1]
          Học bổng nhiều [1,3,1] | Danh tiếng [1,1,3]
needAid "no" | "preferred" | "required"
items   c, g2, blk ("MAIN" | "AID"), F, H, Q, gap, pg, ps, adj, r
KEY VẮNG MẶT = THIẾU DỮ LIỆU.
## CÁC BƯỚC
P1 Trọng số: tong = pref[0] + pref[1] + pref[2]
     tong = 0 → w = [1/3, 1/3, 1/3]; còn lại w = pref / tong
P2 Điểm xếp hạng, chỉ trường blk = "MAIN":
     X = w[0] × F + w[1] × H + w[2] × Q
     Tiêu chí nào thiếu thì bỏ, chia lại w cho các tiêu chí còn lại.
     Làm tròn X 3 chữ số.
P3 Chia khối:
     reach  = MAIN có g2 "R" hoặc "RF", tối đa 3
     match  = MAIN có g2 "M", tối đa 5
     safety = MAIN có g2 "S", tối đa 4
     aid    = các trường blk "AID", chỉ khi needAid khác "no"
     other  = trường có g2 "NA" hoặc "O"
P4 Sắp xếp rồi mới cắt theo số tối đa:
     reach, match, safety: X giảm dần, bằng X thì mã tăng dần
     aid: gap tăng dần
P5 why, theo đúng thứ tự, bỏ mục nào không có số:
     1. pg > 75 GPA_HIGH | 25 ≤ pg ≤ 75 GPA_MID | pg < 25 GPA_LOW
     2. ps > 75 SAT_ABOVE | 25 ≤ ps ≤ 75 SAT_IN_RANGE | ps < 25 SAT_BELOW
     3. blk MAIN → BUDGET_OK | blk AID → NEED_AID
     4. r < 0.5 → INTL_LOW; nếu không: adj > 0 → EC_PLUS,
        adj < 0 → EC_MINUS, adj = 0 thì bỏ
## VÍ DỤ
Đầu vào:
{"pref":[2,2,2],"needAid":"preferred","items":[
 {"c":"190099","g2":"RF","blk":"AID","gap":45610,"pg":17.2,"ps":0,
  "adj":0,"r":0.25},
 {"c":"B","g2":"S","blk":"MAIN","F":0.24,"H":0.105,"Q":0.8,
  "pg":70,"ps":87.5,"adj":0},
 {"c":"C","g2":"M","blk":"MAIN","F":0.08,"H":0.261,"Q":0.9,
  "pg":50,"ps":58.33,"adj":0}]}
P1 tong = 6 → w = [1/3, 1/3, 1/3]
P2 B: X = (0.24 + 0.105 + 0.8) / 3 = 0.382
   C: X = (0.08 + 0.261 + 0.9) / 3 = 0.414
P3 C → match; B → safety; Colgate (AID) → aid
P5 Colgate: pg 17.2 → GPA_LOW; ps 0 → SAT_BELOW; NEED_AID; r 0.25 → INTL_LOW
Đầu ra:
{"status":"OK","reach":[],
 "match":[{"c":"C","X":0.414,"why":["GPA_MID","SAT_IN_RANGE","BUDGET_OK"]}],
 "safety":[{"c":"B","X":0.382,"why":["GPA_MID","SAT_ABOVE","BUDGET_OK"]}],
 "aid":[{"c":"190099","why":["GPA_LOW","SAT_BELOW","NEED_AID","INTL_LOW"]}],
 "other":[]}
## QUY TẮC
- Chỉ dùng mã trường có trong items. Không thêm trường, không đổi g2.
- Mỗi trường xuất hiện nhiều nhất 1 lần.
- Chỉ trả về JSON đúng khuôn, không viết chữ nào khác.
