// Ảnh khuôn viên dự phòng cho trang đăng nhập/đăng ký (AuthSplitLayout) và trang phương pháp (MethodologyView).
// Trang chủ (/) lấy ảnh bìa và logo trực tiếp từ DB qua /api/v1/universities.

export interface LandingSchool {
  name: string;
  city: string;
  image: string;
}

export const SAMPLE_PROFILE = {
  gpa: 3.6,
  sat: 1380,
  budgetUsd: 40000,
};

export const LANDING_SCHOOLS: LandingSchool[] = [
  { name: "Harvard University", city: "Cambridge, MA", image: "/schools/harvard.jpg" },
  { name: "Stanford University", city: "Stanford, CA", image: "/schools/stanford.jpg" },
  { name: "Massachusetts Institute of Technology", city: "Cambridge, MA", image: "/schools/mit.jpg" },
  { name: "University of California, Berkeley", city: "Berkeley, CA", image: "/schools/berkeley.jpg" },
  { name: "University of Washington", city: "Seattle, WA", image: "/schools/uw.jpg" },
  { name: "University of Michigan", city: "Ann Arbor, MI", image: "/schools/michigan.jpg" },
  { name: "Purdue University", city: "West Lafayette, IN", image: "/schools/purdue.jpg" },
  { name: "Arizona State University", city: "Tempe, AZ", image: "/schools/asu.jpg" },
  { name: "Princeton University", city: "Princeton, NJ", image: "/schools/princeton.jpg" },
  { name: "Williams College", city: "Williamstown, MA", image: "/schools/williams.jpg" },
];
