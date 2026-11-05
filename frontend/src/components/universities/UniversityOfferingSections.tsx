"use client";

import { useState } from "react";
import { CoinsIcon, FileTextIcon, GraduationCapIcon, HandCoinsIcon, HeartHandshakeIcon, HomeIcon, InfoIcon, UsersIcon, WalletIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Card, CardContent } from "@/components/ui/card";
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from "@/components/ui/collapsible";
import { Separator } from "@/components/ui/separator";
import type { UniversityMajor, UniversityOffering } from "@/types/university";
import { annualPublishedCost, MISSING_UNIVERSITY_DATA, schoolAidPolicy, schoolImportanceLabel, schoolSatPolicy, scoreRange, universityDate, universityDeadlines, universityMoney, universityNumber, universityPercent } from "@/lib/university-display";
import { DetailRows, DetailSection, GridDataItem, SourceLink } from "./UniversityDetailParts";

export function UniversityAdmissions({ offering }: { offering: UniversityOffering }) {
  const deadlines = universityDeadlines(offering.deadlinesJson);
  return (
    <DetailSection id="admissions" title="Tuyển sinh" description="Yêu cầu và thống kê đã được lưu trong hệ thống; không phải mức điểm bảo đảm trúng tuyển.">
      <div className="mb-5"><Badge variant="secondary">{schoolSatPolicy(offering.satPolicy)}</Badge></div>
      <DetailRows rows={[
        { label: "GPA tối thiểu (thang 4)", value: universityNumber(offering.minGpa4) },
        { label: "GPA trung bình (thang 4)", value: universityNumber(offering.avgGpa4) },
        { label: "SAT · khoảng 25%–75%", value: scoreRange(offering.sat25, offering.sat75) },
        { label: "ACT · khoảng 25%–75%", value: scoreRange(offering.act25, offering.act75) },
        { label: "IELTS tối thiểu", value: universityNumber(offering.minIelts) },
        { label: "TOEFL iBT tối thiểu", value: universityNumber(offering.minToefl) },
        { label: "Duolingo tối thiểu", value: universityNumber(offering.minDuolingo) },
        { label: "Mức coi trọng GPA", value: schoolImportanceLabel(offering.c7GpaImportance) },
        { label: "Mức coi trọng điểm thi", value: schoolImportanceLabel(offering.c7TestImportance) },
        { label: "Mức coi trọng ngoại khóa", value: schoolImportanceLabel(offering.c7EcImportance) },
      ]} />
      <h3 className="mb-4 mt-7 text-sm font-semibold">Các đợt nộp hồ sơ</h3>
      {deadlines.length ? (
        <div className="grid gap-x-8 gap-y-5 sm:grid-cols-2 md:grid-cols-3">
          {deadlines.map((deadline) => (
            <div key={deadline.round} className="space-y-1">
              <p className="text-xs text-muted-foreground">{deadline.label} ({deadline.round})</p>
              <p className="text-xl font-bold tracking-tight text-foreground">{deadline.date}</p>
            </div>
          ))}
        </div>
      ) : <p className="text-sm text-muted-foreground">{MISSING_UNIVERSITY_DATA}</p>}
      <Alert className="mt-4 bg-muted/30"><InfoIcon /><AlertDescription className="text-xs leading-relaxed">Chính sách và hạn nộp có thể thay đổi theo năm. Hãy xác nhận lại trên website chính thức trước khi nộp hồ sơ.</AlertDescription></Alert>
    </DetailSection>
  );
}

export function UniversityCosts({ offering }: { offering: UniversityOffering }) {
  const total = annualPublishedCost(offering);

  const costItems = [
    { icon: GraduationCapIcon, label: "Học phí", value: universityMoney(offering.tuitionUsd) },
    { icon: HomeIcon, label: "Sinh hoạt phí", value: universityMoney(offering.livingUsd) },
    { icon: FileTextIcon, label: "Phí bắt buộc", value: universityMoney(offering.feesUsd) },
    { icon: WalletIcon, label: "Chi phí khác", value: universityMoney(offering.otherExpensesUsd) },
  ];

  const aidItems = [
    { icon: HeartHandshakeIcon, label: "Chính sách hỗ trợ", value: schoolAidPolicy(offering.intlAidPolicy) },
    { icon: HandCoinsIcon, label: "Mức hỗ trợ trung bình được ghi nhận", value: universityMoney(offering.intlAidAvgUsd) },
    { icon: UsersIcon, label: "Số sinh viên quốc tế nhận hỗ trợ", value: universityNumber(offering.intlAidRecipients) },
  ];

  return (
    <DetailSection id="costs" title="Chi phí & hỗ trợ tài chính" description={offering.academicYear ? `Số liệu chi phí trong hệ thống · năm học ${offering.academicYear} · USD/năm` : "Số liệu chi phí trong hệ thống · USD/năm"}>
      <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
        {costItems.map((item) => (
          <GridDataItem
            key={item.label}
            icon={item.icon}
            label={item.label}
            value={item.value}
          />
        ))}
      </div>

      {total !== null && (
        <Card size="sm" className="mt-5 bg-brand-wash shadow-none ring-0">
          <CardContent className="flex flex-wrap items-center justify-between gap-2 text-brand-ink">
            <div className="flex items-center gap-2">
              <CoinsIcon className="size-5" />
              <span className="text-sm font-medium">Tổng các khoản trên</span>
            </div>
            <strong className="text-lg">{universityMoney(total)} / năm</strong>
          </CardContent>
        </Card>
      )}

      <p className="mt-4 text-xs leading-relaxed text-muted-foreground">Đây là chi phí công bố, chưa phải chi phí thực trả của từng sinh viên. Không tự suy ra mức học bổng hoặc trừ hỗ trợ tài chính khỏi các khoản trên.</p>

      <h3 className="mb-4 mt-7 text-sm font-semibold">Hỗ trợ cho sinh viên quốc tế</h3>
      <div className="grid grid-cols-1 gap-x-8 gap-y-5 sm:grid-cols-2 lg:grid-cols-3">
        {aidItems.map((item) => (
          <GridDataItem
            key={item.label}
            icon={item.icon}
            label={item.label}
            value={item.value}
          />
        ))}
      </div>
    </DetailSection>
  );
}

export function UniversityStatistics({ offering, internationalStudentCount }: { offering: UniversityOffering; internationalStudentCount: number | null }) {
  return (
    <DetailSection id="statistics" title="Thống kê sinh viên & tuyển sinh" description="Các chỉ số có thể thuộc những kỳ dữ liệu khác nhau. Xem nguồn bên dưới để đối chiếu năm.">
      <DetailRows rows={[
        { label: "Tổng hồ sơ nộp", value: universityNumber(offering.applicantsTotal) },
        { label: "Tổng hồ sơ được nhận", value: universityNumber(offering.admittedTotal) },
        { label: "Hồ sơ quốc tế nộp", value: universityNumber(offering.intlApplicants) },
        { label: "Hồ sơ quốc tế được nhận", value: universityNumber(offering.intlAdmitted) },
        { label: "Tỷ lệ nhận quốc tế", value: universityPercent(offering.intlAcceptanceRate) },
        { label: "Số sinh viên quốc tế", value: universityNumber(internationalStudentCount) },
        { label: "Tỷ lệ nộp điểm SAT", value: universityPercent(offering.satSubmitPct) },
        { label: "Tỷ lệ nộp GPA", value: universityPercent(offering.gpaSubmitPct) },
        { label: "Tỷ lệ tiếp tục học sau năm đầu", value: universityPercent(offering.retentionRate1Yr) },
        { label: "Tỷ lệ tốt nghiệp trong 6 năm", value: universityPercent(offering.gradRate6Yr) },
      ]} />
    </DetailSection>
  );
}

function MajorList({ majors }: { majors: UniversityMajor[] }) {
  return (
    <ul className="divide-y divide-border/70">
      {majors.map((major) => (
        <li key={`${major.cipCode}-${major.name}`} className="flex flex-wrap items-center justify-between gap-2 py-3">
          <div><p className="text-sm font-medium">{major.name}</p>{major.cipCode && <p className="mt-0.5 text-xs text-muted-foreground">CIP {major.cipCode}</p>}</div>
          {major.isStem && <Badge variant="secondary">STEM</Badge>}
        </li>
      ))}
    </ul>
  );
}

export function UniversityMajors({ offering }: { offering: UniversityOffering }) {
  const [expanded, setExpanded] = useState(false);
  return (
    <DetailSection id="majors" title="Ngành học" description="Nhóm ngành có trong dữ liệu hiện tại của bậc học đang chọn.">
      {offering.majors.length ? (
        <Collapsible open={expanded} onOpenChange={setExpanded}>
          <MajorList majors={offering.majors.slice(0, 10)} />
          {offering.majors.length > 10 && (
            <>
              <CollapsibleContent><Separator /><MajorList majors={offering.majors.slice(10)} /></CollapsibleContent>
              <CollapsibleTrigger render={<Button type="button" variant="outline" size="sm" className="mt-4" />}>
                {expanded ? "Thu gọn" : `Xem tất cả ${offering.majors.length} nhóm ngành`}
              </CollapsibleTrigger>
            </>
          )}
        </Collapsible>
      ) : <p className="text-sm text-muted-foreground">{MISSING_UNIVERSITY_DATA}</p>}
      {offering.sourceUrl && <div className="mt-5 space-y-4 text-xs text-muted-foreground"><Separator /><p>Nguồn tổng quan của bậc học: <SourceLink url={offering.sourceUrl} /> · Ngày lấy: {universityDate(offering.retrievedAt)}</p></div>}
    </DetailSection>
  );
}
