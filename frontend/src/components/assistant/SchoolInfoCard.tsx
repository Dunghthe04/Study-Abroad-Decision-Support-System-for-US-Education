"use client";

// Thẻ thông tin một trường trong chat: số liệu thật từ cơ sở dữ liệu (code dựng, không qua AI), nằm trên câu trả lời bằng chữ.
// Số liệu null thì ghi "Chưa có số liệu" thay vì để trống hay đoán.

import type { ReactNode } from "react";
import { formatPct, formatUsd } from "@/lib/recommendation-labels";
import type { SchoolInfoData, SchoolInfoField } from "@/types/recommendation";
import { RememberedLine } from "./RememberedLine";

const FIELD_NAME: Record<SchoolInfoField, string> = {
  cost: "chi phí",
  aid: "học bổng",
  english: "tiếng Anh",
  test: "điểm thi",
  gpa: "GPA",
  acceptance: "tỷ lệ nhận",
  deadline: "hạn nộp",
  outcomes: "tốt nghiệp",
  majors: "ngành",
  ranking: "xếp hạng",
  location: "vị trí",
  website: "website",
};
// Bản tóm tắt có nhiều mục: không coi là học sinh đang hỏi một mục cụ thể
const SUMMARY_FIELDS = 5;
const MAX_MAJORS = 8;
const MAX_RANKINGS = 4;
const NO_DATA = (
  <span className="text-xs font-normal text-ink-3">Chưa có số liệu</span>
);

const pct = (fraction: number) => formatPct(fraction);
const score = (value: number) => value.toLocaleString("vi-VN");

const CONTROL: Record<string, string> = {
  public: "Công lập",
  private: "Tư thục",
};
const AID_POLICY: Record<string, string> = {
  none: "Không có học bổng cho du học sinh",
  need_only: "Theo nhu cầu tài chính",
  merit_only: "Theo thành tích",
  need_and_merit: "Theo nhu cầu và thành tích",
};
const TEST_POLICY: Record<string, string> = {
  required: "Bắt buộc nộp SAT hoặc ACT",
  optional: "Không bắt buộc (test-optional)",
  not_accepted: "Không nhận SAT, ACT",
};

function formatDate(raw: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(raw);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : raw;
}

function Row({
  label,
  children,
  note,
}: {
  label: string;
  children: ReactNode;
  note?: ReactNode;
}) {
  return (
    <div className="min-w-0 space-y-0.5">
      <p className="text-[11px] font-medium text-ink-2">{label}</p>
      <div className="text-sm leading-snug font-bold text-foreground">
        {children}
      </div>
      {note && <p className="text-[11px] font-normal text-ink-3">{note}</p>}
    </div>
  );
}

function FieldRow({
  info,
  field,
}: {
  info: SchoolInfoData;
  field: SchoolInfoField;
}) {
  switch (field) {
    case "cost": {
      const c = info.cost;
      const parts = [
        c.tuition != null ? `Học phí ${formatUsd(c.tuition)}` : null,
        c.living != null ? `Sinh hoạt ${formatUsd(c.living)}` : null,
        c.fees != null && c.fees > 0 ? `Phí ${formatUsd(c.fees)}` : null, // 0 không rõ là không có phí hay đã gồm trong học phí
        c.other != null ? `Khác ${formatUsd(c.other)}` : null,
      ].filter(Boolean);
      return (
        <Row
          label="Chi phí mỗi năm"
          note={parts.length > 0 ? parts.join(" · ") : undefined}
        >
          {c.total != null ? formatUsd(c.total) : NO_DATA}
        </Row>
      );
    }
    case "aid": {
      const a = info.aid;
      const policy = a.policy ? AID_POLICY[a.policy] : null;
      const average =
        a.policy !== "none" && a.avgUsd != null
          ? `Người được nhận trung bình ${formatUsd(a.avgUsd)} mỗi năm${a.recipients != null ? ` (${a.recipients} người)` : ""}. Không phải mức bạn sẽ nhận.`
          : undefined;
      return (
        <Row label="Học bổng cho du học sinh" note={average}>
          {policy ?? NO_DATA}
        </Row>
      );
    }
    case "english": {
      const e = info.english;
      const tests = [
        e.minIelts != null ? `IELTS ${score(e.minIelts)}` : null,
        e.minToefl != null ? `TOEFL ${score(e.minToefl)}` : null,
        e.minDuolingo != null ? `Duolingo ${score(e.minDuolingo)}` : null,
      ].filter(Boolean);
      return (
        <Row
          label="Tiếng Anh tối thiểu"
          note={tests.length > 1 ? "Một bài là đủ" : undefined}
        >
          {tests.length > 0 ? tests.join(" · ") : NO_DATA}
        </Row>
      );
    }
    case "test": {
      const t = info.test;
      const ranges = [
        t.sat25 != null && t.sat75 != null ? `SAT ${t.sat25}–${t.sat75}` : null,
        t.act25 != null && t.act75 != null ? `ACT ${t.act25}–${t.act75}` : null,
      ].filter(Boolean);
      return (
        <Row
          label="Điểm thi"
          note={
            ranges.length > 0
              ? `${ranges.join(" · ")} (từ mốc 25% đến 75% của sinh viên trúng tuyển)`
              : undefined
          }
        >
          {(t.satPolicy && TEST_POLICY[t.satPolicy]) || NO_DATA}
        </Row>
      );
    }
    case "gpa": {
      const g = info.gpa;
      // Ba nhóm tách rời, cộng lại 100%: đạt 4.0, từ 3,75 đến 3,99, dưới 3,75
      const note =
        g?.shareAbove375 != null &&
        g.shareFour != null &&
        g.shareAbove375 >= g.shareFour &&
        g.shareAbove375 <= 1
          ? `${pct(g.shareFour)} đạt 4.0 · ${pct(g.shareAbove375 - g.shareFour)} từ 3,75 đến 3,99 · ${pct(1 - g.shareAbove375)} dưới 3,75`
          : g?.shareFour != null
            ? `${pct(g.shareFour)} có GPA 4.0`
            : undefined;
      return (
        <Row label="GPA của sinh viên trúng tuyển" note={note}>
          {g?.avg != null ? `${score(g.avg)} (thang 4)` : NO_DATA}
        </Row>
      );
    }
    case "acceptance": {
      const a = info.admission;
      return (
        <Row
          label="Tỷ lệ nhận"
          note={
            a.intlAcceptRate != null
              ? `Du học sinh: ${pct(a.intlAcceptRate)}`
              : undefined
          }
        >
          {a.acceptRate != null ? pct(a.acceptRate) : NO_DATA}
        </Row>
      );
    }
    case "deadline":
      return (
        <Row
          label="Hạn nộp hồ sơ"
          note="Hạn có thể đổi, bạn kiểm tra lại trên trang của trường."
        >
          {info.deadlines.length > 0
            ? info.deadlines
                .map((d) => `${d.round} ${formatDate(d.date)}`)
                .join(" · ")
            : NO_DATA}
        </Row>
      );
    case "outcomes": {
      const o = info.outcomes;
      return (
        <Row
          label="Tốt nghiệp trong 6 năm"
          note={
            o.retentionRate1Yr != null
              ? `${pct(o.retentionRate1Yr)} học tiếp năm hai`
              : undefined
          }
        >
          {o.gradRate6Yr != null ? pct(o.gradRate6Yr) : NO_DATA}
        </Row>
      );
    }
    case "majors": {
      const shown = info.majors.slice(0, MAX_MAJORS);
      const more = info.majors.length - shown.length;
      return (
        <Row label="Ngành học (một số)">
          {shown.length > 0 ? (
            <span className="font-normal">
              {shown.join(", ")}
              {more > 0 ? ` và ${more} ngành khác` : ""}
            </span>
          ) : (
            NO_DATA
          )}
        </Row>
      );
    }
    case "ranking": {
      const shown = info.rankings.slice(0, MAX_RANKINGS);
      return (
        <Row
          label="Xếp hạng"
          note={
            info.rankings.length > shown.length
              ? `Và ${info.rankings.length - shown.length} bảng khác`
              : undefined
          }
        >
          {shown.length > 0 ? (
            <ul className="space-y-0.5 font-normal">
              {shown.map((r) => (
                <li key={r.listName}>
                  <b className="font-bold">{r.rankDisplay || `#${r.rank}`}</b>
                  {r.isTie && !r.rankDisplay.toLowerCase().includes("tie")
                    ? " (đồng hạng)"
                    : ""}{" "}
                  {r.listName}
                </li>
              ))}
            </ul>
          ) : (
            NO_DATA
          )}
        </Row>
      );
    }
    default:
      return null; // vị trí và website nằm ở đầu thẻ
  }
}

export function SchoolInfoCard({
  info,
  fields,
  remember = false,
}: {
  info: SchoolInfoData;
  fields: SchoolInfoField[];
  remember?: boolean;
}) {
  const place = [info.city, info.state].filter(Boolean).join(", ");
  const control = info.control ? CONTROL[info.control] : null;
  const rows = fields.filter((f) => f !== "location" && f !== "website");
  const source = [
    info.sourceUrl ? `nguồn ${info.sourceUrl}` : null,
    info.retrievedAt ? `cập nhật ${formatDate(info.retrievedAt)}` : null,
  ]
    .filter(Boolean)
    .join(", ");
  return (
    <div className="@container mt-2.5 space-y-3 rounded-xl border border-line bg-surface p-3 text-sm">
      <div className="space-y-0.5">
        <p className="text-[13px] font-bold text-brand">{info.name}</p>
        {(place || control) && (
          <p className="text-xs text-ink-2">
            {[place, control].filter(Boolean).join(" · ")}
          </p>
        )}
        {info.website && (
          <a
            href={info.website}
            target="_blank"
            rel="noreferrer"
            className="text-xs text-brand underline underline-offset-2"
          >
            {info.website.replace(/^https?:\/\//, "")}
          </a>
        )}
      </div>
      {rows.length > 0 && (
        <div className="grid grid-cols-1 gap-x-4 gap-y-2.5 @sm:grid-cols-2">
          {rows.map((field) => (
            <FieldRow key={field} info={info} field={field} />
          ))}
        </div>
      )}
      {rows.includes("cost") && (
        <p className="text-[11px] text-ink-3">
          Chi phí có thể thay đổi, bạn nên kiểm tra lại trên trang chính thức
          của trường.
        </p>
      )}
      {source && <p className="text-[11px] text-ink-3">Số liệu: {source}.</p>}
      {remember && (
        <RememberedLine
          about={[
            info.name,
            fields.length > 0 && fields.length < SUMMARY_FIELDS
              ? fields.map((f) => FIELD_NAME[f] ?? f).join(", ")
              : null,
          ]
            .filter(Boolean)
            .join(" · ")}
          hint="Bạn hỏi tiếp, ví dụ “còn học bổng?” hoặc “còn trường khác thì sao?”."
        />
      )}
    </div>
  );
}
