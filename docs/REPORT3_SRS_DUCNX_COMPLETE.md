# TÀI LIỆU ĐẶC TẢ YÊU CẦU PHẦN MỀM (SRS) - CHUẨN FORM GOOGLE DOCS
## PHÂN HỆ: STUDENT PROFILE & ACADEMIC ANALYSIS MODULE (USAS-364 & USAS-365)
**Sinh viên phụ trách:** Nguyễn Xuân Đức (`ducnxhe186870@fpt.edu.vn`)  
**Đồ án:** USAS – Study Abroad Decision Support System for US Education  
**Thời gian cập nhật:** 06/10/2026  

---

# II. Use Case Specifications

### 1.3 Manage Financial Profile & Extracurricular Activities

| Section | Detail Specification |
| :--- | :--- |
| **Primary Actors** | Student / Applicant |
| **Secondary Actors** | None |
| **Description** | As a user, I want to manage my educational financial profile, extracurricular activities, and academic achievements so that the system can assess my financial feasibility and holistic profile competitiveness for US university admissions. |
| **Preconditions** | User account has been created & authorized, and user has logged into the system. |
| **Normal Flow** | **Manage Financial Profile & Extracurricular Activities**<br>1. User clicks Financial & Activities menu from the page header or navigation bar.<br>2. System shows the Financial & Activities Profile management screen (`/profile/financial`).<br>3. User types in financial information (annual educational budget in USD, primary funding source, scholarship requirement).<br>4. User types in extracurricular activities (activity title, role/organization, impact level 1–5, duration months, description).<br>5. User types in academic achievements & honors (award title, issuing organization, issue date, description).<br>6. User clicks the Save Profile button (or individual add buttons).<br>7. System validates financial details, activity information, and award fields against business rules (GBR-FIN-01, GBR-FIN-02, GBR-ACT-01).<br>8. System saves the financial profile and activities/awards into the database (`student_profiles`, `profile_activities`).<br>9. System tracks user's profile update to the Activity Log.<br>10. System shows the success notification message (MSG-FIN-SUCCESS / MSG-ACT-SUCCESS) and refreshes the displayed portfolio without page reload. |
| **Alternative Flows** | **Step 3.1_Zero budget declaration**<br>1. User types in $0 for the annual educational budget.<br>2. System checks whether Needs Scholarship is flagged or funding source is Scholarship.<br>3. System displays an informational note that a zero-budget profile relies on full scholarships or need-based financial aid.<br>4. Return to step 4 of normal flow.<br><br>**Step 4.1_Delete an existing extracurricular activity or award**<br>1. User clicks Delete button on a specific activity or award item in the list.<br>2. System displays a confirmation popup.<br>3. User confirms the deletion.<br>4. System dispatches `DELETE /api/v1/profile/activities/{id}` and removes the record from the database (HTTP 204 No Content).<br>5. System updates the activity list instantly without reloading the page.<br><br>**Step 6.1_System can't save the financial profile**<br>User can't save financial profile & gets relevant error message in one of below cases:<br>• Annual budget is left blank or non-numeric (MSG-FIN-ERR-BUDGET).<br>• Annual budget is a negative value or exceeds 10,000,000 USD (MSG-FIN-ERR-BUDGET).<br>• Primary Funding Source is unselected.<br><br>**Step 6.2_System can't save extracurricular activity or award**<br>User can't save activity/award & gets relevant error message in one of below cases:<br>• Activity title or Award title is left blank or less than 2 characters (MSG-ACT-ERR-TITLE).<br>• Activity title or Award title exceeds 150 characters.<br>• Impact level is not within 1 to 5.<br>• Description exceeds 1,000 characters.<br><br>**Step 6.3_System connection error**<br>If system encounters unexpected database or network failure while saving data (HTTP 500), system shows error message (MSG-NET-RETRY) and keeps all entered data in the form for user to retry. |
| **Postconditions** | • User saves financial profile, extracurricular activities, and awards successfully.<br>• The system tracked profile update into the Activity Log. |

---

### 1.4 Academic Performance Analysis & GPA Conversion

| Section | Detail Specification |
| :--- | :--- |
| **Primary Actors** | Student |
| **Secondary Actors** | None |
| **Description** | As a user, I want to input my transcript grades across semesters (high school or college) and trigger automated academic evaluation so that I can view my WES 4.0 standard GPA, subject group proficiencies, and 3-year academic growth trends. |
| **Preconditions** | User account has been created & authorized, and user has logged into the system. |
| **Normal Flow** | **Academic Performance Analysis & GPA Conversion**<br>1. User clicks Academic Analysis menu from the page header or navigation bar.<br>2. System shows the Academic Records & Analysis screen (`/profile/academic`) with education level switcher (High School vs. University/College).<br>3. User selects an academic semester (e.g., Grade 10 Semester 1 through Grade 12 Semester 2) and types in subject name, grade on 0.00 – 10.00 scale, and optional credit weights.<br>4. User clicks Add Subject to append course rows, or clicks Load Standard Template to fast-fill 3-year curriculum.<br>5. User clicks the "Phân tích điểm học thuật" (Analyze Academic Profile) button.<br>6. System validates the academic scores, score boundaries, and semester sequence (GBR-ACAD-01).<br>7. System calculates cumulative raw average on 10.0 scale.<br>8. System converts Vietnamese grades to US 4.0 standard GPA following WES standards (GBR-ACAD-02) configured in `app_settings`.<br>9. System calculates Unweighted GPA (arithmetic mean) and Weighted GPA (credit-weighted or honors/advanced course +0.5 bonus up to 4.50) (GBR-ACAD-03).<br>10. System computes subject group scores: STEM Average, Social Sciences Average, and Languages Average.<br>11. System evaluates the multi-term academic trajectory (Upward trend, Consistent, Downward trend, or Insufficient data) (GBR-ACAD-04).<br>12. System saves calculated metrics into `analysis_results` (`kind = 'gpa'`) and automatically synchronizes `student_profiles.overall_gpa` (GBR-ACAD-05).<br>13. System tracks calculation and analysis results into the Activity Log.<br>14. System shows the comprehensive Executive Summary & Analytics Dashboard (WES 4.0 GPA, Subject Group Breakdown chart, Term Trend Chart, and Growth Mindset Badge) with success message (MSG-ANALYZE-OK). |
| **Alternative Flows** | **Step 3.1_Fast fill from standard high school curriculum template**<br>1. User clicks "Nạp dữ liệu mẫu THPT" (Load Standard Template) button.<br>2. System populates standard MOET 3-year curriculum (25 courses across 6 semesters) with default credits.<br>3. User reviews the pre-filled subject rows and types in or updates semester grades.<br>4. Return to step 5 of normal flow.<br><br>**Step 3.2_University / College degree mode with credits weighting**<br>1. User switches education level to "Đại học (Tín chỉ)".<br>2. System switches term selectors to flexible semester ordering (Term 1 to 20) and highlights course credits input.<br>3. Weighted GPA is strictly computed based on credit-weighted summation: $\frac{\sum (GPA4_i \times Credits_i)}{\sum Credits_i}$.<br>4. Return to step 5 of normal flow.<br><br>**Step 11.1_Profiles with fewer than 2 academic terms**<br>1. If student enters transcript data for only 1 semester (`totalTerms < 2`), linear regression cannot determine a trajectory.<br>2. System displays a neutral status badge: **"Chưa đủ dữ liệu xu hướng"** (MSG-TREND-NOT-ENOUGH) instead of misclassifying the trend.<br>3. Return to step 12 of normal flow.<br><br>**Step 6.1_System can't perform academic analysis**<br>User can't trigger analysis & gets relevant error message in one of below cases:<br>• Subject score field is blank while subject row exists.<br>• Input score is less than 0.00 or greater than 10.00 (MSG-SCORE-ERR-RANGE).<br>• Input score contains non-numeric characters.<br>• Input duplicate subject within the same academic semester (MSG-SCORE-ERR-DUP).<br>• Input semester has 0 course scores.<br><br>**Step 6.2_Decimal score precision handling**<br>• System accepts fractional decimal scores (e.g., 8.25, 7.75, 6.85) using `step="any"` input without native browser validation rejection.<br><br>**Step 6.3_System connection error & failure recovery**<br>• If API returns HTTP 500 or network drops while loading, system displays error alert with a "Thử tải lại dữ liệu" retry button (MSG-NET-RETRY), completely separating error state from empty form.<br>• If API returns HTTP 500 while saving, system retains all entered subject rows and scores in the form so the student does not have to retype. |
| **Postconditions** | • User saves transcript scores and views academic analysis results successfully.<br>• The system synchronized Unweighted GPA to `student_profiles.overall_gpa`.<br>• The system tracked academic evaluation into the Activity Log. |

---

# III. Functional Requirements

## 1. Student Profile & Academic Analysis Module

### 1.3 Financial & Extracurricular Profile Management

#### 1.3.1 Screen: Financial & Extracurricular Profile
* **Content #1: UI layout (Wireframe or Mockup for the screen)**  
  *(To be added later)*

* **Content #2: Screen Description (Mapped to Use Case)**  
  * **Mapped Use Case:** 1.3 Manage Financial Profile & Extracurricular Activities (UC-03)
  * **This screen allows the Student to:**
    * **View Financial & Activities Data:** View current declared annual budget, funding source, scholarship flag, list of extracurricular activities, and academic honors.
    * **Update Financial Profile:** Enter/edit expected annual budget in USD, select primary funding channel, and declare financial aid requirements.
    * **Manage Extracurricular Activities:** Add, view, and delete extracurricular engagements with title, organization role, timeframe, impact level (1–5), and contribution description.
    * **Manage Achievements & Awards:** Add, view, and delete academic awards, national/international honors, certified credentials, and research projects.

* **Content #3: Field Description Table**

| Field Name | Description |
| :--- | :--- |
| **Group: Financial Information - Input fields for student financial capability declaration** | |
| `AnnualBudgetUsd` | • Data type: Positive decimal number.<br>• Range / Min-Max: Min: `0`, Max: `10,000,000` USD.<br>• Initial / Default value: `30,000` (or previously saved value).<br>• Requirement: Required. Entering `0` indicates reliance on full scholarships or need-based financial aid. |
| `FundingSource` | • Data type: Dropdown selector.<br>• Domain values: `Family Support` (`family_support`), `Personal Savings` (`personal_savings`), `Bank Loan` (`bank_loan`), `Scholarship` (`scholarship`), `Other` (`other`).<br>• Initial value: `Family Support`.<br>• Requirement: Required. Declares primary educational funding channel. |
| `NeedsScholarship` | • Data type: Boolean checkbox.<br>• Initial value: `true` (Checked).<br>• Requirement: Optional. Flags applicant profile to prioritize institutions offering international scholarships. |
| `BtnSaveFinancial` | • Component: Action button.<br>• Behavior: Validates financial input against business rules (GBR-FIN-01, GBR-FIN-02) and persists data into `student_profiles` via `POST /api/v1/profile/financial`. |
| **Group: Extracurricular Activities - Input fields for extracurricular engagements and leadership** | |
| `ActivityTitle` | • Data type: Text string.<br>• Length: 2 – 150 characters.<br>• Initial value: Blank.<br>• Requirement: Required. Name of extracurricular activity, club, volunteer project, or sports team. |
| `OrganizationRole` | • Data type: Text string.<br>• Max length: 100 characters.<br>• Initial value: Blank.<br>• Requirement: Optional. Sponsoring organization name and leadership or participant role held (e.g., "Chủ nhiệm CLB", "Trưởng ban Hậu cần"). |
| `ImpactLevel` | • Data type: Dropdown selector.<br>• Domain values: `1: Cấp Trường / CLB`, `2: Cấp Quận / Huyện`, `3: Cấp Tỉnh / Thành phố`, `4: Cấp Quốc gia`, `5: Cấp Quốc tế`.<br>• Initial value: `1`.<br>• Requirement: Required (GBR-ACT-01). Scope of influence for holistic admission scoring. |
| `DurationMonths` | • Data type: Positive integer number.<br>• Range: 1 – 120 months.<br>• Initial value: Blank.<br>• Requirement: Optional. Total duration of commitment in months. |
| `ActivityDescription` | • Data type: Textarea string.<br>• Max length: 1,000 characters.<br>• Initial value: Blank.<br>• Requirement: Optional. Detailed description of responsibilities, initiatives, and measurable outcomes. |
| `BtnAddActivity` | • Component: Action button.<br>• Behavior: Validates input and persists activity record into `profile_activities` (`kind = 'extracurricular'`). |
| `BtnDeleteActivity` | • Component: Icon action button.<br>• Behavior: Dispatches `DELETE /api/v1/profile/activities/{id}` to remove activity record (HTTP 204 No Content). |
| **Group: Achievements & Awards - Input fields for academic honors, certifications, and research** | |
| `AwardTitle` | • Data type: Text string.<br>• Length: 2 – 150 characters.<br>• Initial value: Blank.<br>• Requirement: Required. Title of competition award, scholastic honor, standardized credential, or research publication (e.g., "Giải Nhì HSG Quốc Gia môn Toán", "Chứng chỉ SAT 1520"). |
| `Issuer` | • Data type: Text string.<br>• Max length: 150 characters.<br>• Initial value: Blank.<br>• Requirement: Required. Issuing organization, contest committee, or university (e.g., "Bộ Giáo dục và Đào tạo", "College Board"). |
| `IssueDate` | • Data type: Text / Month-Year picker.<br>• Format: MM/YYYY.<br>• Initial value: Blank.<br>• Requirement: Optional. Month and year of award receipt or certificate issuance. |
| `AwardDescription` | • Data type: Textarea string.<br>• Max length: 1,000 characters.<br>• Initial value: Blank.<br>• Requirement: Optional. Details regarding prize ranking (First Place, Gold Medal, Top 1%), score breakdown, or paper topic. |
| `BtnAddAward` | • Component: Action button.<br>• Behavior: Validates input and persists award record into `profile_activities` (`kind = 'award'`). |
| `BtnDeleteAward` | • Component: Icon action button.<br>• Behavior: Dispatches `DELETE /api/v1/profile/activities/{id}` to remove award record (HTTP 204 No Content). |

---

### 1.4 Academic Performance Analysis & GPA Dashboard

#### 1.4.1 Screen: Academic Performance Analysis & GPA Dashboard
* **Content #1: UI layout (Wireframe or Mockup for the screen)**  
  *(To be added later)*

* **Content #2: Screen Description (Mapped to Use Case)**  
  * **Mapped Use Case:** 1.4 Academic Performance Analysis & GPA Conversion (UC-04)
  * **This screen allows the Student to:**
    * **Switch Education Level:** Toggle between "THPT (Cấp 3)" and "Đại học (Tín chỉ)" to adapt input mode to secondary school subjects or university credit weighting.
    * **Input Transcript Grades:** Select academic semesters (Grade 10 Semester 1 through Grade 12 Semester 2 or custom terms 1–20) and record individual subject course grades on the Vietnamese 10.0 scale.
    * **Autofill Standard Template:** Click the "Nạp dữ liệu mẫu THPT" button to pre-fill standard high school curriculum courses (25 courses) for rapid evaluation and testing.
    * **Execute GPA Conversion & Analytics:** Trigger automated evaluation to convert transcript grades into standardized US WES 4.0 GPA benchmarks.
    * **View Academic Competitiveness:** Review Executive Summary (Unweighted GPA, Weighted GPA up to 4.50, Raw Average 10.0), performance breakdown across subject clusters (STEM, Social Sciences, Languages), and the responsive Term Trend Chart.

* **Content #3: Field Description Table**

| Field Name | Description |
| :--- | :--- |
| **Group: Education Level & Term Settings - Input controls for academic context** | |
| `EducationLevelSwitcher` | • Component: Segmented Control / Radio switch.<br>• Domain values: `THPT (Cấp 3)`, `Đại học (Tín chỉ)`.<br>• Initial value: `THPT (Cấp 3)`.<br>• Requirement: Required. Adapts UI between 3-year high school curriculum and university credit-weighted courses. |
| `SemesterSelect` | • Data type: Dropdown selector.<br>• Domain values: Standard 6 semesters (`Lớp 10 HK1` to `Lớp 12 HK2`) or custom term sequence (`Kỳ 1` to `Kỳ 20`).<br>• Initial value: `Lớp 10 HK1`.<br>• Requirement: Required. Determines chronological term sequence (`term_order` 1 to 20). |
| `IsCustomTermToggle` | • Data type: Boolean checkbox.<br>• Initial value: `false`.<br>• Requirement: Optional. Enables custom semester name and order for non-standard academic calendars. |
| **Group: Transcript Grade Entry - Input components for course records and scores** | |
| `SubjectName` | • Data type: Text string with autocomplete suggestions.<br>• Length: 1 – 100 characters.<br>• Requirement: Required. Subject course title (e.g., Toán, Vật lý, Hóa học, Sinh học, Ngữ văn, Lịch sử, Địa lý, Tiếng Anh). |
| `Score10` | • Data type: Decimal number with up to 2 decimal places.<br>• Range / Min-Max: Min: `0.00`, Max: `10.00`.<br>• Input step: `step="any"` (fully accepts fractional decimal scores such as `8.25` or `7.75` without native browser step mismatch errors).<br>• Requirement: Required (GBR-ACAD-01). Raw subject course grade on Vietnamese 10.0 scale. |
| `CreditsWeight` | • Data type: Positive decimal number.<br>• Range: `[0.5, 10.0]`.<br>• Initial / Default value: `1.0` (Standard THPT course weight) or actual university course credits.<br>• Requirement: Optional. Course credit multiplier used in Weighted GPA calculation. |
| `BtnAddSubject` | • Component: Action button.<br>• Behavior: Validates score boundaries and appends course record into active semester transcript table. Retains form input upon server error. |
| `BtnLoadTemplate` | • Component: Helper button.<br>• Behavior: Auto-populates standard 3-year high school curriculum (25 courses across 6 terms) for rapid testing. |
| `BtnAnalyze` | • Component: Primary Action button.<br>• Behavior: Dispatches request to `POST /api/v1/profile/academic/analyze` to execute WES 4.0 conversion, subject grouping, and trajectory classification. |
| **Group: Executive Summary & Academic Metrics - Calculated indicators and visualization components** | |
| `UnweightedGpa` | • Component: Read-only numeric display.<br>• Format: Standard US 4.0 scale formatted to 2 decimal places (e.g., `3.65 / 4.0`).<br>• Calculation: Simple arithmetic mean across all graded subjects converted to WES 4.0 scale. |
| `WeightedGpa` | • Component: Read-only numeric display.<br>• Format: Unbounded upper score display up to **`4.50`** (e.g., `4.15` kèm ghi chú "Tín chỉ / môn nâng cao", không gò bó mẫu số `/ 4.0`).<br>• Calculation: Credit-weighted mean incorporating advanced/honors course bonus points (+0.5). |
| `VietnameseGpa` | • Component: Read-only numeric display.<br>• Format: Vietnamese 10.0 scale (e.g., `8.82 / 10.0`).<br>• Calculation: Cumulative grade point average across all submitted semesters. |
| `TrendBadge` | • Component: Visual status badge.<br>• Domain values / Display:<br>  - `Đà tăng trưởng (Growth Mindset)` (Green badge, slope $\ge +0.05$)<br>  - `Phong độ ổn định` (Blue badge, $-0.05 < slope < +0.05$)<br>  - `Đang có sự giảm sút` (Amber badge, slope $\le -0.05$)<br>  - `Chưa đủ dữ liệu xu hướng` (Neutral slate badge, displayed when recorded terms $< 2$).<br>• Behavior: Evaluates academic growth trajectory based on linear regression slope across recorded terms (GBR-ACAD-04). |
| `SubjectGroupBars` | • Component: Graphical progress bar breakdown.<br>• Metrics displayed: Average 10.0 score, converted 4.0 GPA, subject count, and distinct course tags across three academic clusters: STEM, Languages, and Social Sciences. |
| `TermTrendChart` | • Component: Bar chart visualization.<br>• Behavior: Renders semester-by-semester average GPA progression. Features internal horizontal scroll (`min-w-[280px]`) preventing mobile viewport overflow (375px/768px), 2-line semester labels preventing truncation, and true baseline rendering (0 GPA renders at 0% baseline with legible text). |
| `WESDisclaimer` | • Component: Informational alert notice.<br>• Behavior: States that grade conversions strictly follow World Education Services (WES) US standards for guidance, while individual US institutions retain autonomous evaluation policies. |
| `ErrorRetryBanner` | • Component: Alert error state container.<br>• Behavior: Displayed when initial data fetch fails (HTTP 500/network error); replaces empty state and provides a "Thử tải lại dữ liệu" retry button without showing misleading empty state. |
