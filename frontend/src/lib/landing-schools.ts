// Danh sách trường mẫu cho trang chủ: ảnh nền phần đầu trang và mục "Một số trường học sinh Việt Nam hay hỏi".
// Muốn đổi trường hay ảnh thì sửa thẳng mảng bên dưới. Ảnh để trong public/schools, ngang, rộng 1920px;
// ảnh hiện tại lấy từ Wikimedia Commons, `credit` ghi tác giả và giấy phép để hiện ở chân trang.
// `group` và `acceptRate` chỉ để minh họa cách đặt nhóm cạnh tỷ lệ nhận chung, không phải kết quả thật của ai.

export type SchoolType = "private" | "public" | "liberal";

export interface LandingSchool {
  name: string;
  city: string;
  type: SchoolType;
  typeLabel: string;
  image: string; // ảnh khuôn viên, dùng cho cả ảnh nền và thẻ trường
  logo?: string; // logo trường (Cloudinary của hệ thống), hiện ở dải logo chạy ngang
  credit?: string; // tác giả và giấy phép ảnh, hiện một dòng ở chân trang
  highlight: string;
  majors: string[];
  acceptRate: number; // 0–1, số công bố gần nhất
  group: "R" | "M" | "S"; // nhóm minh họa với hồ sơ mẫu GPA 3.6 · SAT 1380 · 40.000 USD/năm
  inHero?: boolean; // có xuất hiện ở ảnh nền đầu trang không
}

export const SAMPLE_PROFILE = {
  gpa: 3.6,
  sat: 1380,
  budgetUsd: 40000,
};

export const LANDING_SCHOOLS: LandingSchool[] = [
  {
    name: "Harvard University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801647/usdss/universities/166027/UNI_166027_logo.webp",
    city: "Cambridge, MA",
    type: "private",
    typeLabel: "Ivy League",
    image: "/schools/harvard.jpg",
    credit: "Widener Library, chensiyuan, CC BY-SA 4.0",
    highlight: "Xét hồ sơ need-blind cả với sinh viên quốc tế và đáp ứng toàn bộ nhu cầu tài chính được duyệt.",
    majors: ["Computer Science", "Economics", "Applied Math"],
    acceptRate: 0.036,
    group: "R",
    inHero: true,
  },
  {
    name: "Stanford University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801708/usdss/universities/243744/UNI_243744_logo.webp",
    city: "Stanford, CA",
    type: "private",
    typeLabel: "Tư thục",
    image: "/schools/stanford.jpg",
    credit: "Memorial Church, Frank Schulenburg, CC BY-SA 4.0",
    highlight: "Nằm giữa Thung lũng Silicon, gần các công ty công nghệ lớn.",
    majors: ["AI & Data Science", "Management", "Bioengineering"],
    acceptRate: 0.037,
    group: "R",
  },
  {
    name: "Massachusetts Institute of Technology",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801419/usdss/universities/166683/UNI_166683_logo.webp",
    city: "Cambridge, MA",
    type: "private",
    typeLabel: "Tư thục",
    image: "/schools/mit.jpg",
    credit: "Great Dome, Beyond My Ken, CC BY-SA 4.0",
    highlight: "Đứng đầu về Kỹ thuật và Khoa học Máy tính, xét need-blind với sinh viên quốc tế.",
    majors: ["Computer Science", "Electrical Eng", "Physics"],
    acceptRate: 0.045,
    group: "R",
  },
  {
    name: "University of California, Berkeley",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801612/usdss/universities/110635/UNI_110635_logo.webp",
    city: "Berkeley, CA",
    type: "public",
    typeLabel: "Công lập",
    image: "/schools/berkeley.jpg",
    credit: "Sather Tower, Antoine Taveneaux, CC BY-SA 3.0",
    highlight: "Trường công lập hàng đầu, gần San Francisco, mạnh về Kỹ thuật và Kinh tế.",
    majors: ["EECS", "Economics", "Data Science"],
    acceptRate: 0.11,
    group: "R",
  },
  {
    name: "University of Washington",
    city: "Seattle, WA",
    type: "public",
    typeLabel: "Công lập",
    image: "/schools/uw.jpg",
    credit: "Suzzallo Library, Martin Kraft, CC BY-SA 3.0",
    highlight: "Mạnh về Khoa học Máy tính, ở ngay Seattle nên dễ tìm chỗ thực tập.",
    majors: ["Computer Science", "Bioengineering", "Information Systems"],
    acceptRate: 0.43,
    group: "M",
    inHero: true,
  },
  {
    name: "University of Michigan",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801634/usdss/universities/170976/UNI_170976_logo.webp",
    city: "Ann Arbor, MI",
    type: "public",
    typeLabel: "Công lập",
    image: "/schools/michigan.jpg",
    credit: "Law Quadrangle, w_lemay, CC BY-SA 2.0",
    highlight: "Trường công lập lớn, nhiều ngành mạnh từ Kinh doanh đến Kỹ thuật.",
    majors: ["Business", "Mechanical Eng", "Computer Science"],
    acceptRate: 0.18,
    group: "R",
  },
  {
    name: "Purdue University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801723/usdss/universities/243780/UNI_243780_logo.webp",
    city: "West Lafayette, IN",
    type: "public",
    typeLabel: "Công lập",
    image: "/schools/purdue.jpg",
    credit: "Engineering Fountain, Julian Herzog, CC BY 4.0",
    highlight: "Nổi tiếng về Kỹ thuật và Hàng không vũ trụ, học phí thấp hơn trường tư.",
    majors: ["Aerospace Eng", "Mechanical Eng", "Cybersecurity"],
    acceptRate: 0.5,
    group: "M",
    inHero: true,
  },
  {
    name: "Arizona State University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801541/usdss/universities/104151/UNI_104151_logo.webp",
    city: "Tempe, AZ",
    type: "public",
    typeLabel: "Công lập",
    image: "/schools/asu.jpg",
    credit: "Old Main, Beyond My Ken, CC BY-SA 4.0",
    highlight: "Nhiều ngành, có học bổng theo thành tích cho sinh viên quốc tế.",
    majors: ["Software Engineering", "Business", "Data Science"],
    acceptRate: 0.9,
    group: "S",
    inHero: true,
  },
  {
    name: "Princeton University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801610/usdss/universities/186131/UNI_186131_logo.webp",
    city: "Princeton, NJ",
    type: "private",
    typeLabel: "Ivy League",
    image: "/schools/princeton.jpg",
    credit: "Nassau Hall, Ken Lund, CC BY-SA 2.0",
    highlight: "Tập trung vào bậc cử nhân, hỗ trợ tài chính bằng học bổng thay vì khoản vay.",
    majors: ["Economics", "Public Policy", "Computer Science"],
    acceptRate: 0.045,
    group: "R",
  },
  {
    name: "Williams College",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801494/usdss/universities/168342/UNI_168342_logo.webp",
    city: "Williamstown, MA",
    type: "liberal",
    typeLabel: "Liberal Arts",
    image: "/schools/williams.jpg",
    credit: "West College, Beyond My Ken, CC BY-SA 4.0",
    highlight: "Lớp nhỏ, giảng viên theo sát từng sinh viên, mạnh về nghiên cứu bậc cử nhân.",
    majors: ["Economics", "Mathematics", "Political Science"],
    acceptRate: 0.1,
    group: "R",
  },
];

export const HERO_SCHOOLS = LANDING_SCHOOLS.filter((s) => s.inHero);

// Dòng ghi công ảnh ở chân trang (giấy phép CC BY/BY-SA yêu cầu ghi tác giả)
export const PHOTO_CREDITS = LANDING_SCHOOLS.filter((s) => s.credit).map((s) => s.credit).join(" · ");

// Dải logo chạy ngang dưới phần đầu trang: các trường có logo, thêm Columbia chỉ để làm dài dải
export const LOGO_STRIP: { name: string; logo: string }[] = [
  ...LANDING_SCHOOLS.filter((s): s is LandingSchool & { logo: string } => Boolean(s.logo)).map((s) => ({ name: s.name, logo: s.logo })),
  {
    name: "Columbia University",
    logo: "https://res.cloudinary.com/kse7jkd6/image/upload/v1793801529/usdss/universities/190150/UNI_190150_logo.webp",
  },
];
