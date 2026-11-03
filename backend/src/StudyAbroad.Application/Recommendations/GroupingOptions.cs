namespace StudyAbroad.Application.Recommendations;

// Tham số của công thức xếp nhóm trường. Mọi con số đều chỉnh được, không ghi cứng trong hàm tính.
public record GroupingOptions(
    double LowSubmitPct = 0.5,       // tỉ lệ sinh viên nộp SAT dưới mốc này thì SAT ít đại diện
    double LowSubmitFactor = 0.5,    // khi đó trọng số điểm thi nhân hệ số này
    double EcStep = 0.05,            // p đổi bao nhiêu cho mỗi bậc ngoại khóa lệch khỏi mức trung bình
    double EcCap = 0.10,             // trần tổng hiệu chỉnh ngoại khóa (cộng hoặc trừ)
    double LowerCut = 0.25,          // p' dưới mốc này: Khó vào
    double UpperCut = 0.75,          // p' từ mốc này: An toàn
    double AlwaysReachRate = 0.15,   // tỉ lệ nhận dưới mốc này: luôn Khó vào
    double SafetyMinRate = 0.50,     // tỉ lệ nhận dưới mốc này: không được là An toàn
    double IntlRatioCut = 0.5);      // tỉ lệ nhận du học sinh / tỉ lệ chung dưới mốc này: hạ một nhóm
