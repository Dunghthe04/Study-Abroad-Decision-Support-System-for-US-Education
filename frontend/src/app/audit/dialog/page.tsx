"use client";

import React, { useState } from "react";
import {
  AlertTriangleIcon,
  BanIcon,
  BookOpenIcon,
  Building2Icon,
  CalendarClockIcon,
  CheckCircle2Icon,
  ClipboardCheckIcon,
  ClockIcon,
  CoinsIcon,
  FileSpreadsheetIcon,
  FileTextIcon,
  FlagIcon,
  GraduationCapIcon,
  HistoryIcon,
  ImageIcon,
  InfoIcon,
  KeyRoundIcon,
  LayersIcon,
  LockIcon,
  MailIcon,
  MessageSquareIcon,
  RotateCcwIcon,
  ScaleIcon,
  SchoolIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
  SparklesIcon,
  Trash2Icon,
  UnlockIcon,
  UserMinusIcon,
  UserPlusIcon,
  UsersIcon,
} from "lucide-react";

import {
  AppDialog,
  Dialog,
  DialogAction,
  DialogBody,
  DialogCancel,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogIcon,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogBody,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogIcon,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldDescription, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";

import { BirthDateRequestDialog } from "@/components/account/BirthDateRequestDialog";
import {
  RevokeAndDeleteChildDialog,
  RevokeConsentDialog,
} from "@/components/account/ParentConsentDialogs";
import {
  CenterActionConfirmDialog,
  type CenterConfirmAction,
} from "@/components/admin/centers/CenterActionConfirmDialog";
import {
  CenterLegalHoldDialog,
  type LegalHoldAction,
} from "@/components/admin/center-deletion/CenterLegalHoldDialog";
import { KnowledgeImportResultDialog } from "@/components/admin/knowledge-base/KnowledgeImportResultDialog";
import { SlmImportResultDialog } from "@/components/admin/slm-samples/SlmImportResultDialog";
import { ConsultationBookingModal } from "@/components/centers/ConsultationBookingModal";

import type { StudyCenter, UserDto } from "@/types/api";
import type { KnowledgeImportResult } from "@/types/knowledge-base";
import type { SlmImportResult } from "@/types/slm-sample";

// ponytail: single-file audit workbench, all mock data & handlers inline

const MOCK_USER: UserDto = {
  id: "u-audit-01",
  email: "minh.nguyen@example.com",
  fullName: "Nguyễn Văn Minh",
  role: "student",
  status: "active",
  dateOfBirth: "2008-05-14",
  ageGroup: "minor",
  parentConsentStatus: "granted",
};

const MOCK_CENTER: StudyCenter = {
  id: "c-audit-01",
  code: "USAS-HN-01",
  name: "Trung tâm Tư vấn Du học Minh Anh",
  website: "https://minhanh.edu.vn",
  address: "120 Trần Duy Hưng, Cầu Giấy",
  city: "Hà Nội",
  studyLevels: ["undergraduate", "master"],
  services: ["Tư vấn chọn trường", "Sửa bài luận", "Luyện phỏng vấn Visa"],
  surveyedAt: "2026-01-15T00:00:00Z",
  taxCode: "0108123456",
  legalRepresentative: "Trần Thị Lan",
  licenseNumber: "GP-1234/SGDĐT",
  verificationStatus: "pending",
  isActive: true,
};

const MOCK_KB_IMPORT_RESULT: KnowledgeImportResult = {
  totalRows: 42,
  created: 28,
  updated: 10,
  unchanged: 2,
  vectorsStarted: true,
  vectorWarning: null,
  errors: [
    { rowNumber: 14, message: "Thiếu trường topicCode cho docType=proc_steps" },
    { rowNumber: 29, message: "URL nguồn không đúng định dạng https://" },
  ],
};

const MOCK_SLM_IMPORT_RESULT: SlmImportResult = {
  totalRows: 60,
  inserted: 52,
  duplicates: 5,
  errors: [
    { sheetName: "scenario", rowNumber: 8, message: "intents không được để trống" },
    { sheetName: "knowledge", rowNumber: 23, message: "split phải thuộc train | val | test" },
    { sheetName: "profile_review", rowNumber: 41, message: "JSON profile không hợp lệ" },
  ],
};

type SectionItem = {
  id: string;
  label: string;
  badge?: string;
  onClick: () => void;
};

export default function DialogAuditPage() {
  const [active, setActive] = useState<string | null>(null);
  const [statusBanner, setStatusBanner] = useState<string | null>(null);

  // Interactive states for specific dialogs
  const [loadingSim, setLoadingSim] = useState(false);
  const [alertReason, setAlertReason] = useState("");
  const [deleteConfirmText, setDeleteConfirmText] = useState("");
  const [edBinding, setEdBinding] = useState(false);
  const [targetInlineDelete, setTargetInlineDelete] = useState(false);
  const [reviewReportReason, setReviewReportReason] = useState("spam");
  const [jsonExpanded, setJsonExpanded] = useState(false);
  const [birthReviewRejecting, setBirthReviewRejecting] = useState(false);
  const [birthReviewRejectReason, setBirthReviewRejectReason] = useState("");

  // Real component states
  const [birthDateOpen, setBirthDateOpen] = useState(false);
  const [revokeConsentOpen, setRevokeConsentOpen] = useState(false);
  const [revokeDeleteOpen, setRevokeDeleteOpen] = useState(false);
  const [centerAction, setCenterAction] = useState<CenterConfirmAction | null>(null);
  const [legalHoldAction, setLegalHoldAction] = useState<LegalHoldAction | null>(null);
  const [kbImportResult, setKbImportResult] = useState<KnowledgeImportResult | null>(null);
  const [slmImportResult, setSlmImportResult] = useState<SlmImportResult | null>(null);

  const notify = (msg: string) => {
    setStatusBanner(msg);
  };

  const closeActive = (msg?: string) => {
    setActive(null);
    if (msg) notify(msg);
  };

  const triggerLoadingSim = () => {
    setLoadingSim(true);
    setTimeout(() => {
      setLoadingSim(false);
      setActive(null);
      notify("Loading simulation finished (2s) and closed.");
    }, 2000);
  };

  const sections: { title: string; items: SectionItem[] }[] = [
    {
      title: "1. Design Spec Primitives (Image #1 & Image #2)",
      items: [
        { id: "p-pair", label: "Action Dialog — Pair (Tint + Destructive)", onClick: () => setActive("p-pair") },
        { id: "p-single", label: "Action Dialog — Single (I understood)", onClick: () => setActive("p-single") },
        { id: "p-stack", label: "Action Dialog — Stack (3 options)", onClick: () => setActive("p-stack") },
        { id: "p-split", label: "Action Dialog — Split-Top", onClick: () => setActive("p-split") },
        { id: "p-ghost", label: "Action Dialog — Ghost Cancel + Primary", onClick: () => setActive("p-ghost") },
        { id: "p-loading", label: "Action Dialog — Loading / Non-dismissible (2s)", onClick: () => setActive("p-loading") },
        { id: "p-form", label: "Form Dialog — Basic (2 inputs)", onClick: () => setActive("p-form") },
        { id: "p-long-form", label: "Form Dialog — Long Scrollable Form", onClick: () => setActive("p-long-form") },
        { id: "p-alert-action", label: "AlertDialog — Centered Action", onClick: () => setActive("p-alert-action") },
        { id: "p-alert-form", label: "AlertDialog — Form with Reason Textarea", onClick: () => { setAlertReason(""); setActive("p-alert-form"); } },
      ],
    },
    {
      title: "2. Account & Policy",
      items: [
        { id: "acc-policy-gate", label: "Policy Gate (Blocking Terms, Stack)", onClick: () => setActive("acc-policy-gate") },
        { id: "acc-birthdate", label: "BirthDateRequestDialog", badge: "REAL", onClick: () => setBirthDateOpen(true) },
        { id: "acc-delete", label: "Delete Account ('XÓA' typed confirm)", onClick: () => { setDeleteConfirmText(""); setActive("acc-delete"); } },
        { id: "acc-fam-invite", label: "Family Invite Member (Form)", onClick: () => setActive("acc-fam-invite") },
        { id: "acc-fam-remove", label: "Family Leave / Remove (AlertDialog)", onClick: () => setActive("acc-fam-remove") },
        { id: "acc-revoke-consent", label: "RevokeConsentDialog", badge: "REAL", onClick: () => setRevokeConsentOpen(true) },
        { id: "acc-revoke-delete", label: "RevokeAndDeleteChildDialog", badge: "REAL", onClick: () => setRevokeDeleteOpen(true) },
        { id: "acc-parent-email", label: "Parent Invite / Change Email (Form)", onClick: () => setActive("acc-parent-email") },
        { id: "acc-parent-unlink", label: "Parent Unlink Confirm (Action)", onClick: () => setActive("acc-parent-unlink") },
        { id: "acc-lead-thread", label: "Sent Lead Messages Thread (Detail)", onClick: () => setActive("acc-lead-thread") },
      ],
    },
    {
      title: "3. Parent, Profile, Flow & Costs",
      items: [
        { id: "ppf-budget", label: "Parent Budget Dialog (Form)", onClick: () => setActive("ppf-budget") },
        { id: "ppf-add-child", label: "Add Managed Child (Form)", onClick: () => setActive("ppf-add-child") },
        { id: "ppf-suggest-uni", label: "Suggest University to Children (Form)", onClick: () => setActive("ppf-suggest-uni") },
        { id: "ppf-tx-add", label: "Transcript: Add Subject (Form)", onClick: () => setActive("ppf-tx-add") },
        { id: "ppf-tx-reassign", label: "Transcript: Reassign Grade (Multi-option)", onClick: () => setActive("ppf-tx-reassign") },
        { id: "ppf-tx-delete", label: "Transcript: Switch / Delete Confirm", onClick: () => setActive("ppf-tx-delete") },
        { id: "ppf-profile-del", label: "Profile: Delete Achievement Confirm", onClick: () => setActive("ppf-profile-del") },
        { id: "ppf-cost-settings", label: "Cost Estimate: Budget & Scholarship (Single Close)", onClick: () => setActive("ppf-cost-settings") },
      ],
    },
    {
      title: "4. Target Schools, Recommendations, Centers & Community",
      items: [
        { id: "ts-add", label: "Add Target School (ED binding checkbox)", onClick: () => { setEdBinding(false); setActive("ts-add"); } },
        { id: "ts-manage", label: "Manage Saved Target School (Inline Delete Toggle)", onClick: () => { setTargetInlineDelete(false); setActive("ts-manage"); } },
        { id: "ts-delete", label: "Target School: Delete Confirm", onClick: () => setActive("ts-delete") },
        { id: "ts-category", label: "Target School: Change Category (3 options)", onClick: () => setActive("ts-category") },
        { id: "ts-round", label: "Target School: Change Application Round", onClick: () => setActive("ts-round") },
        { id: "rec-view", label: "Recommendation History: View Run (xl)", onClick: () => setActive("rec-view") },
        { id: "rec-compare", label: "Recommendation History: Compare Runs (xl table)", onClick: () => setActive("rec-compare") },
        { id: "scen-save", label: "Saved Scenarios: Save Form", onClick: () => setActive("scen-save") },
        { id: "scen-list", label: "Saved Scenarios: Scenarios List", onClick: () => setActive("scen-list") },
        { id: "scen-del", label: "Saved Scenarios: Delete Confirm", onClick: () => setActive("scen-del") },
        { id: "ct-form", label: "Contact Request: Form View", onClick: () => setActive("ct-form") },
        { id: "ct-age-block", label: "Contact Request: Age-block View", onClick: () => setActive("ct-age-block") },
        { id: "ct-success", label: "Contact Request: Success View", onClick: () => setActive("ct-success") },
        { id: "rev-remove", label: "Center Review: Remove Review", onClick: () => setActive("rev-remove") },
        { id: "rev-report", label: "Center Review: Report Review (Radio + Textarea)", onClick: () => setActive("rev-report") },
        { id: "cp-delete", label: "Center Portal: Deletion Request", onClick: () => setActive("cp-delete") },
        { id: "cp-legal-change", label: "Center Portal: Legal Field Change Confirm", onClick: () => setActive("cp-legal-change") },
        { id: "cp-lead-chat", label: "Center Portal: Lead Detail & Chat (xl)", onClick: () => setActive("cp-lead-chat") },
        { id: "comm-report", label: "Community Chat: Report Message", onClick: () => setActive("comm-report") },
        { id: "comm-lightbox", label: "Community Chat: Image Lightbox (full dark)", onClick: () => setActive("comm-lightbox") },
      ],
    },
    {
      title: "5. Admin & Moderator",
      items: [
        { id: "adm-lock", label: "Admin Users: Lock User", onClick: () => setActive("adm-lock") },
        { id: "adm-unlock", label: "Admin Users: Unlock User (tone='ok')", onClick: () => setActive("adm-unlock") },
        { id: "adm-reset-2fa", label: "Admin Users: Reset 2FA", onClick: () => setActive("adm-reset-2fa") },
        { id: "adm-log-detail", label: "Access Log Detail (lg + expandable JSON)", onClick: () => { setJsonExpanded(false); setActive("adm-log-detail"); } },
        { id: "adm-verif-queue", label: "Center Verification Queue (2xl)", onClick: () => setActive("adm-verif-queue") },
        { id: "adm-verif-review", label: "Review Verification (xl, split-top)", onClick: () => setActive("adm-verif-review") },
        { id: "adm-ca-verify", label: "CenterActionConfirmDialog: verify", badge: "REAL", onClick: () => setCenterAction("verify") },
        { id: "adm-ca-unverify", label: "CenterActionConfirmDialog: unverify", badge: "REAL", onClick: () => setCenterAction("unverify") },
        { id: "adm-ca-hide", label: "CenterActionConfirmDialog: hide", badge: "REAL", onClick: () => setCenterAction("hide") },
        { id: "adm-ca-show", label: "CenterActionConfirmDialog: show", badge: "REAL", onClick: () => setCenterAction("show") },
        { id: "adm-cpc-fail", label: "Center Profile Changes: Mark Failed & Hide", onClick: () => setActive("adm-cpc-fail") },
        { id: "adm-crr-action", label: "Center Review Reports: Remove / Dismiss", onClick: () => setActive("adm-crr-action") },
        { id: "adm-cdr-action", label: "Center Deletion Requests: Approve / Reject / Extend", onClick: () => setActive("adm-cdr-action") },
        { id: "adm-lh-set", label: "CenterLegalHoldDialog: Enable", badge: "REAL", onClick: () => setLegalHoldAction("set") },
        { id: "adm-lh-release", label: "CenterLegalHoldDialog: Release", badge: "REAL", onClick: () => setLegalHoldAction("release") },
        { id: "adm-birth-review", label: "Birth Date Request Review (lg, split-top)", onClick: () => { setBirthReviewRejecting(false); setBirthReviewRejectReason(""); setActive("adm-birth-review"); } },
        { id: "adm-lead-mod", label: "Lead Message Reports: Moderation & Context", onClick: () => setActive("adm-lead-mod") },
        { id: "adm-billing-plan", label: "Billing: Delete Plan & Order Reject/Activate", onClick: () => setActive("adm-billing-plan") },
        { id: "adm-grade-save", label: "Parameters: Grade Scale Save Confirm", onClick: () => setActive("adm-grade-save") },
        { id: "adm-grade-hist", label: "Parameters: Grade Scale History (lg)", onClick: () => setActive("adm-grade-hist") },
        { id: "adm-policy-pub", label: "Policies: Publish / Discard Draft", onClick: () => setActive("adm-policy-pub") },
        { id: "adm-policy-read", label: "Policies: Policy Version Reader (xl)", onClick: () => setActive("adm-policy-read") },
        { id: "adm-kb-del", label: "KB & SLM: Delete Confirm", onClick: () => setActive("adm-kb-del") },
        { id: "adm-kb-restore", label: "KB & SLM: Restore Version", onClick: () => setActive("adm-kb-restore") },
        { id: "adm-kb-import", label: "KB & SLM: Excel Import (full)", onClick: () => setActive("adm-kb-import") },
        { id: "adm-kb-result", label: "KnowledgeImportResultDialog", badge: "REAL", onClick: () => setKbImportResult(MOCK_KB_IMPORT_RESULT) },
        { id: "adm-slm-result", label: "SlmImportResultDialog", badge: "REAL", onClick: () => setSlmImportResult(MOCK_SLM_IMPORT_RESULT) },
      ],
    },
  ];

  const totalCount = sections.reduce((sum, s) => sum + s.items.length, 0) + 1; // +1 for ConsultationBookingModal

  return (
    <div className="min-h-screen bg-bg text-ink px-4 py-8 sm:px-8">
      <div className="mx-auto max-w-6xl space-y-8">
        <header className="flex flex-wrap items-center justify-between gap-4 border-b border-line pb-5">
          <div>
            <h1 className="font-heading text-2xl font-semibold tracking-tight text-ink">
              Dialog Audit Workbench
            </h1>
            <p className="mt-1 text-sm text-ink-2">
              Click any button below to spawn and visually audit every dialog pattern and standalone component.
            </p>
          </div>
          <Badge variant="secondary" className="text-sm px-3 py-1">
            {totalCount} dialogs
          </Badge>
        </header>

        {statusBanner && (
          <Alert variant="info" className="flex items-center justify-between">
            <AlertDescription>{statusBanner}</AlertDescription>
            <Button size="sm" variant="ghost" onClick={() => setStatusBanner(null)}>
              Dismiss
            </Button>
          </Alert>
        )}

        {sections.map((section) => (
          <section key={section.title} className="space-y-3">
            <h2 className="font-heading text-base font-semibold text-ink">
              {section.title}
            </h2>
            <div className="flex flex-wrap gap-2">
              {section.items.map((item) => (
                <Button
                  key={item.id}
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={item.onClick}
                  className="gap-1.5"
                >
                  <span>{item.label}</span>
                  {item.badge && (
                    <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">
                      {item.badge}
                    </Badge>
                  )}
                </Button>
              ))}
              {section.title.startsWith("4.") && (
                <ConsultationBookingModal centerName={MOCK_CENTER.name} />
              )}
            </div>
          </section>
        ))}
      </div>

      {/* ==================== 1. DESIGN SPEC PRIMITIVES ==================== */}
      <AppDialog
        open={active === "p-pair"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Delete item permanently?"
        description="This action cannot be undone. All associated records will be removed."
        cancelLabel="Cancel"
        cancelTone="tint"
        confirmLabel="Delete"
        confirmTone="destructive"
        onConfirm={() => closeActive("Confirmed destructive pair dialog")}
      />

      <AppDialog
        open={active === "p-single"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<InfoIcon />}
        iconTone="brand"
        title="Session settings updated"
        description="Your preferences have been synchronized across all connected devices."
        cancelLabel={null}
        confirmLabel="I understood"
        confirmTone="primary"
        onConfirm={() => closeActive("Closed single action dialog")}
      />

      <Dialog open={active === "p-stack"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="brand">
              <LayersIcon />
            </DialogIcon>
            <DialogTitle>Choose export format</DialogTitle>
            <DialogDescription>Select how you would like to download this report.</DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="primary" onClick={() => closeActive("Selected Option A")}>
              Option A — PDF Document
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Selected Option B")}>
              Option B — Excel Spreadsheet
            </DialogAction>
            <DialogCancel tone="ghost">Option C — Cancel</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={active === "p-split"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="warning">
              <AlertTriangleIcon />
            </DialogIcon>
            <DialogTitle>Updated privacy policy</DialogTitle>
            <DialogDescription>
              Review the updated data processing terms before continuing to use the advisor.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter layout="split-top">
            <DialogAction tone="primary-tint" onClick={() => notify("Opened policy details")}>
              Read more
            </DialogAction>
            <DialogCancel tone="tint">Cancel</DialogCancel>
            <DialogAction tone="primary" onClick={() => closeActive("Accepted split-top dialog")}>
              Accept
            </DialogAction>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AppDialog
        open={active === "p-ghost"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<CheckCircle2Icon />}
        iconTone="success"
        title="Publish changes now?"
        description="The updated guide will immediately become visible to all students."
        cancelLabel="Cancel"
        cancelTone="ghost"
        confirmLabel="Publish"
        confirmTone="primary"
        onConfirm={() => closeActive("Confirmed ghost cancel + primary")}
      />

      <AppDialog
        open={active === "p-loading"}
        onOpenChange={(o) => !o && !loadingSim && closeActive()}
        variant="action"
        icon={<ClockIcon />}
        iconTone="brand"
        title="Syncing advisor index"
        description="Click Start to simulate a 2-second non-dismissible loading state."
        cancelLabel="Cancel"
        confirmLabel="Start 2s Sync"
        confirmTone="primary"
        loading={loadingSim}
        dismissible={!loadingSim}
        onConfirm={triggerLoadingSim}
      />

      <AppDialog
        open={active === "p-form"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        title="Basic Form Dialog"
        description="Enter basic details below and click Save."
        cancelLabel="Cancel"
        cancelTone="tint"
        confirmLabel="Save"
        confirmTone="primary"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Saved basic form");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="pf-1">Input 1</FieldLabel>
            <Input id="pf-1" placeholder="Enter primary label..." defaultValue="Fall 2027 Intake" />
          </Field>
          <Field>
            <FieldLabel htmlFor="pf-2">Input 2</FieldLabel>
            <Input id="pf-2" placeholder="Enter secondary note..." />
          </Field>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "p-long-form"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="md"
        title="Long Scrollable Form"
        description="Verifies sticky header, scrollable DialogBody, and sticky footer."
        cancelLabel="Cancel"
        confirmLabel="Save All"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Saved long scrollable form");
        }}
      >
        <FieldGroup className="gap-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <Field key={i}>
              <FieldLabel htmlFor={`lf-${i}`}>Field #{i + 1}</FieldLabel>
              <Input id={`lf-${i}`} placeholder={`Value for field ${i + 1}`} />
            </Field>
          ))}
        </FieldGroup>
      </AppDialog>

      <AlertDialog open={active === "p-alert-action"} onOpenChange={(o) => !o && closeActive()}>
        <AlertDialogContent variant="action">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <ShieldAlertIcon />
            </AlertDialogIcon>
            <AlertDialogTitle>Revoke API access?</AlertDialogTitle>
            <AlertDialogDescription>
              Existing integrations using this key will immediately fail authentication.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction tone="destructive" onClick={() => closeActive("AlertDialog action confirmed")}>
              Revoke Key
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={active === "p-alert-form"} onOpenChange={(o) => !o && closeActive()}>
        <AlertDialogContent variant="form">
          <AlertDialogHeader>
            <AlertDialogIcon tone="warning">
              <AlertTriangleIcon />
            </AlertDialogIcon>
            <AlertDialogTitle>Provide reason for action</AlertDialogTitle>
            <AlertDialogDescription>
              This reason will be recorded in the audit trail.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogBody>
            <Textarea
              rows={3}
              value={alertReason}
              onChange={(e) => setAlertReason(e.target.value)}
              placeholder="Enter reason (required)..."
            />
          </AlertDialogBody>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              tone="destructive"
              disabled={!alertReason.trim()}
              onClick={() => closeActive(`AlertDialog form confirmed: ${alertReason}`)}
            >
              Confirm
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* ==================== 2. ACCOUNT & POLICY ==================== */}
      <Dialog open={active === "acc-policy-gate"} onOpenChange={() => {}}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="brand">
              <FileTextIcon />
            </DialogIcon>
            <DialogTitle>Điều khoản sử dụng mới</DialogTitle>
            <DialogDescription>
              Bạn cần đồng ý với Chính sách bảo mật & Điều khoản sử dụng (v2.4) để tiếp tục sử dụng USAS.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="primary" onClick={() => closeActive("Accepted Policy Gate")}>
              Tôi đồng ý và tiếp tục
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => notify("Opened full policy text")}>
              Xem chi tiết điều khoản
            </DialogAction>
            <DialogAction tone="ghost" onClick={() => closeActive("Logged out from Policy Gate")}>
              Đăng xuất
            </DialogAction>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <BirthDateRequestDialog
        open={birthDateOpen}
        onOpenChange={setBirthDateOpen}
        user={MOCK_USER}
        onSubmitted={(_req, msg) => notify(msg)}
      />

      <AppDialog
        open={active === "acc-delete"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa tài khoản vĩnh viễn?"
        description="Tài khoản sẽ bị khóa ngay lập tức và xóa dữ liệu sau 30 ngày. Nhập XÓA để xác nhận."
        cancelLabel="Hủy"
        confirmLabel="Xóa tài khoản"
        confirmTone="destructive"
        confirmDisabled={deleteConfirmText.trim().toUpperCase() !== "XÓA"}
        onConfirm={() => closeActive("Account deletion confirmed")}
      >
        <Field>
          <FieldLabel htmlFor="del-acc-confirm">Nhập &quot;XÓA&quot; để xác nhận</FieldLabel>
          <Input
            id="del-acc-confirm"
            value={deleteConfirmText}
            onChange={(e) => setDeleteConfirmText(e.target.value)}
            placeholder="XÓA"
          />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "acc-fam-invite"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<UserPlusIcon />}
        title="Mời thành viên Family"
        description="Nhập email người thân để chia sẻ quyền lợi gói Family."
        cancelLabel="Hủy"
        confirmLabel="Gửi lời mời"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Sent Family invite");
        }}
      >
        <Field>
          <FieldLabel htmlFor="fam-email">Email thành viên</FieldLabel>
          <Input id="fam-email" type="email" placeholder="nguoithan@example.com" />
        </Field>
      </AppDialog>

      <AlertDialog open={active === "acc-fam-remove"} onOpenChange={(o) => !o && closeActive()}>
        <AlertDialogContent variant="action">
          <AlertDialogHeader>
            <AlertDialogIcon tone="destructive">
              <UserMinusIcon />
            </AlertDialogIcon>
            <AlertDialogTitle>Xóa thành viên khỏi Family?</AlertDialogTitle>
            <AlertDialogDescription>
              Thành viên sẽ mất quyền truy cập Premium ngay khi rời nhóm.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter layout="pair">
            <AlertDialogCancel>Hủy</AlertDialogCancel>
            <AlertDialogAction tone="destructive" onClick={() => closeActive("Removed family member")}>
              Xóa khỏi nhóm
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <RevokeConsentDialog
        open={revokeConsentOpen}
        onCancel={() => setRevokeConsentOpen(false)}
        onConfirm={() => {
          setRevokeConsentOpen(false);
          notify("RevokeConsentDialog confirmed");
        }}
      />

      <RevokeAndDeleteChildDialog
        open={revokeDeleteOpen}
        childName="Nguyễn Minh Anh"
        onCancel={() => setRevokeDeleteOpen(false)}
        onConfirm={() => {
          setRevokeDeleteOpen(false);
          notify("RevokeAndDeleteChildDialog confirmed");
        }}
      />

      <AppDialog
        open={active === "acc-parent-email"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<MailIcon />}
        title="Mời / Đổi email phụ huynh"
        description="Hệ thống sẽ gửi liên kết xác nhận đồng ý giám hộ tới email này."
        cancelLabel="Hủy"
        confirmLabel="Gửi lời mời"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Updated parent email");
        }}
      >
        <Field>
          <FieldLabel htmlFor="par-email">Email phụ huynh</FieldLabel>
          <Input id="par-email" type="email" defaultValue="phuhuynh@example.com" />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "acc-parent-unlink"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<UserMinusIcon />}
        iconTone="destructive"
        title="Hủy liên kết phụ huynh?"
        description="Phụ huynh sẽ không còn xem được hồ sơ học tập và ngân sách của bạn."
        cancelLabel="Giữ liên kết"
        confirmLabel="Hủy liên kết"
        confirmTone="destructive"
        onConfirm={() => closeActive("Unlinked parent")}
      />

      <AppDialog
        open={active === "acc-lead-thread"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="lg"
        icon={<MessageSquareIcon />}
        title="Hội thoại với Trung tâm Minh Anh"
        description="Trạng thái: Đang phản hồi · Gửi lúc 14:20 hôm nay"
        cancelLabel="Đóng"
      >
        <div className="space-y-3 text-sm">
          <div className="rounded-xl bg-surface-2 p-3">
            <p className="font-medium text-ink">Bạn</p>
            <p className="text-ink-2">Em muốn hỏi về lộ trình săn học bổng ngành CS kỳ Thu 2027.</p>
          </div>
          <div className="rounded-xl border border-line p-3">
            <p className="font-medium text-brand">Tư vấn viên Minh Anh</p>
            <p className="text-ink-2">Chào em, trung tâm đã nhận hồ sơ và sẽ gửi danh sách 5 trường phù hợp trong chiều nay.</p>
          </div>
        </div>
      </AppDialog>

      {/* ==================== 3. PARENT, PROFILE, FLOW & COSTS ==================== */}
      <AppDialog
        open={active === "ppf-budget"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<CoinsIcon />}
        title="Thiết lập ngân sách gia đình"
        description="Mức chi tối đa mỗi năm giúp AI lọc trường phù hợp cho con."
        cancelLabel="Hủy"
        confirmLabel="Lưu ngân sách"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Saved parent budget");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="pb-usd">Ngân sách tối đa (USD / năm)</FieldLabel>
            <Input id="pb-usd" type="number" defaultValue="35000" />
          </Field>
          <Field>
            <FieldLabel htmlFor="pb-note">Ghi chú tài chính</FieldLabel>
            <Textarea id="pb-note" rows={2} placeholder="Bao gồm sinh hoạt phí và bảo hiểm..." />
          </Field>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "ppf-add-child"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<UsersIcon />}
        title="Thêm hồ sơ con"
        description="Tạo tài khoản hoặc liên kết hồ sơ học sinh dưới sự giám hộ."
        cancelLabel="Hủy"
        confirmLabel="Tạo hồ sơ"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Added managed child");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="ac-name">Họ và tên con</FieldLabel>
            <Input id="ac-name" placeholder="Nguyễn Minh Anh" />
          </Field>
          <Field>
            <FieldLabel htmlFor="ac-dob">Ngày sinh</FieldLabel>
            <Input id="ac-dob" type="date" defaultValue="2010-08-20" />
          </Field>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "ppf-suggest-uni"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<SchoolIcon />}
        title="Gợi ý trường cho con"
        description="Chọn hồ sơ con và thêm lời nhắn kèm theo trường Arizona State University."
        cancelLabel="Hủy"
        confirmLabel="Gửi gợi ý"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Suggested university to child");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="su-child">Chọn con</FieldLabel>
            <Select defaultValue="c1">
              <SelectTrigger id="su-child" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="c1">Nguyễn Minh Anh (Lớp 11)</SelectItem>
                <SelectItem value="c2">Nguyễn Bảo Khang (Lớp 10)</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="su-msg">Lời nhắn</FieldLabel>
            <Textarea id="su-msg" rows={2} defaultValue="Con xem thử trường này có ngành CS tốt và học phí vừa tầm." />
          </Field>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "ppf-tx-add"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<BookOpenIcon />}
        title="Thêm môn học vào bảng điểm"
        description="Nhập tên môn và điểm tổng kết theo thang điểm hiện hành."
        cancelLabel="Hủy"
        confirmLabel="Thêm môn"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Added transcript subject");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="tx-subj">Tên môn học</FieldLabel>
            <Input id="tx-subj" placeholder="Ví dụ: Toán học" />
          </Field>
          <Field>
            <FieldLabel htmlFor="tx-score">Điểm trung bình</FieldLabel>
            <Input id="tx-score" type="number" step="0.1" defaultValue="8.8" />
          </Field>
        </FieldGroup>
      </AppDialog>

      <Dialog open={active === "ppf-tx-reassign"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="brand">
              <GraduationCapIcon />
            </DialogIcon>
            <DialogTitle>Chuyển khối lớp cho bảng điểm</DialogTitle>
            <DialogDescription>Chọn khối lớp muốn gán lại cho bảng điểm hiện tại.</DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="primary" onClick={() => closeActive("Assigned Grade 10")}>
              Lớp 10
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Assigned Grade 11")}>
              Lớp 11
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Assigned Grade 12")}>
              Lớp 12
            </DialogAction>
            <DialogCancel tone="ghost">Hủy</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AppDialog
        open={active === "ppf-tx-delete"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa bảng điểm Lớp 10?"
        description="Toàn bộ điểm các môn trong học kỳ này sẽ bị xóa khỏi hồ sơ học thuật."
        cancelLabel="Hủy"
        confirmLabel="Xóa bảng điểm"
        confirmTone="destructive"
        onConfirm={() => closeActive("Deleted transcript")}
      />

      <AppDialog
        open={active === "ppf-profile-del"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa thành tích / hoạt động ngoại khóa?"
        description="Mục 'Giải Nhì Tin học trẻ cấp Thành phố' sẽ bị xóa khỏi hồ sơ."
        cancelLabel="Hủy"
        confirmLabel="Xóa"
        confirmTone="destructive"
        onConfirm={() => closeActive("Deleted achievement")}
      />

      <AppDialog
        open={active === "ppf-cost-settings"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<CoinsIcon />}
        title="Cài đặt ngân sách & học bổng"
        description="Điều chỉnh mức hỗ trợ tài chính gia đình và kỳ vọng học bổng."
        cancelLabel="Đóng"
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="cs-fam">Đóng góp gia đình (USD/năm)</FieldLabel>
            <Input id="cs-fam" type="number" defaultValue="30000" />
          </Field>
          <Field>
            <FieldLabel htmlFor="cs-schol">Học bổng kỳ vọng (USD/năm)</FieldLabel>
            <Input id="cs-schol" type="number" defaultValue="12000" />
          </Field>
        </FieldGroup>
      </AppDialog>

      {/* ==================== 4. TARGET SCHOOLS, RECS, CENTERS & COMMUNITY ==================== */}
      <AppDialog
        open={active === "ts-add"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<SchoolIcon />}
        title="Thêm trường mục tiêu"
        description="Lưu trường vào danh sách theo dõi nộp hồ sơ."
        cancelLabel="Hủy"
        confirmLabel="Thêm trường"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive(`Added target school (ED binding=${edBinding})`);
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="ts-name">Tên trường</FieldLabel>
            <Input id="ts-name" defaultValue="University of Washington" />
          </Field>
          <Field>
            <FieldLabel htmlFor="ts-cat">Nhóm trường</FieldLabel>
            <Select defaultValue="target">
              <SelectTrigger id="ts-cat" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="reach">Reach (Thử thách)</SelectItem>
                <SelectItem value="target">Target (Vừa sức)</SelectItem>
                <SelectItem value="safety">Safety (An toàn)</SelectItem>
              </SelectContent>
            </Select>
          </Field>
          <div className="flex items-start gap-2 pt-1">
            <Checkbox
              id="ts-ed-bind"
              checked={edBinding}
              onCheckedChange={(c) => setEdBinding(Boolean(c))}
            />
            <Label htmlFor="ts-ed-bind" className="text-sm leading-snug">
              Nộp Early Decision (ED — ràng buộc nhập học nếu trúng tuyển)
            </Label>
          </div>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "ts-manage"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        title="University of Illinois Urbana-Champaign"
        description="Ngành: Computer Science · Hạn nộp: 01/11/2026 (Early Action)"
        cancelLabel="Đóng"
      >
        <div className="space-y-4">
          <div className="rounded-xl border border-line bg-surface-2 p-3 text-sm space-y-1">
            <p><strong>Nhóm:</strong> Target</p>
            <p><strong>Học phí ước tính:</strong> $36,000 / năm</p>
          </div>
          {!targetInlineDelete ? (
            <Button
              type="button"
              variant="outline"
              className="w-full text-risk border-risk/30 hover:bg-risk-wash"
              onClick={() => setTargetInlineDelete(true)}
            >
              Xóa khỏi danh sách mục tiêu
            </Button>
          ) : (
            <div className="rounded-xl border border-risk/30 bg-risk-wash p-3 space-y-2">
              <p className="text-xs font-medium text-risk">Xác nhận xóa trường này khỏi danh sách?</p>
              <div className="flex gap-2">
                <Button size="sm" variant="outline" onClick={() => setTargetInlineDelete(false)}>
                  Giữ lại
                </Button>
                <Button size="sm" variant="destructive" onClick={() => closeActive("Deleted from inline toggle")}>
                  Xác nhận xóa
                </Button>
              </div>
            </div>
          )}
        </div>
      </AppDialog>

      <AppDialog
        open={active === "ts-delete"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa trường mục tiêu?"
        description="Purdue University sẽ bị gỡ khỏi danh sách trường mục tiêu của bạn."
        cancelLabel="Hủy"
        confirmLabel="Xóa trường"
        confirmTone="destructive"
        onConfirm={() => closeActive("Deleted target school")}
      />

      <Dialog open={active === "ts-category"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="brand">
              <LayersIcon />
            </DialogIcon>
            <DialogTitle>Đổi nhóm trường mục tiêu</DialogTitle>
            <DialogDescription>Chọn phân loại phù hợp với hồ sơ hiện tại.</DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="primary-tint" onClick={() => closeActive("Category -> Reach")}>
              Reach (Thử thách)
            </DialogAction>
            <DialogAction tone="primary" onClick={() => closeActive("Category -> Target")}>
              Target (Vừa sức)
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Category -> Safety")}>
              Safety (An toàn)
            </DialogAction>
            <DialogCancel tone="ghost">Hủy</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={active === "ts-round"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="brand">
              <CalendarClockIcon />
            </DialogIcon>
            <DialogTitle>Đổi đợt nộp hồ sơ</DialogTitle>
            <DialogDescription>Chọn vòng tuyển sinh dự kiến nộp.</DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="primary" onClick={() => closeActive("Round -> EA")}>
              Early Action (EA)
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Round -> ED")}>
              Early Decision (ED)
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Round -> RD")}>
              Regular Decision (RD)
            </DialogAction>
            <DialogCancel tone="ghost">Hủy</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AppDialog
        open={active === "rec-view"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="xl"
        icon={<SparklesIcon />}
        title="Chi tiết lần gợi ý #14"
        description="Chạy lúc 10:15 · 28/11/2026 · GPA 8.7 · IELTS 7.5 · Ngân sách $38,000/năm"
        cancelLabel="Đóng"
      >
        <div className="space-y-3 text-sm">
          <div className="rounded-xl border border-line p-3">
            <strong>1. University of Minnesota Twin Cities</strong> — Target · $35,200/năm · Phù hợp 91%
          </div>
          <div className="rounded-xl border border-line p-3">
            <strong>2. Arizona State University</strong> — Safety · $32,800/năm · Phù hợp 95%
          </div>
          <div className="rounded-xl border border-line p-3">
            <strong>3. University of Wisconsin-Madison</strong> — Reach · $40,600/năm · Phù hợp 78%
          </div>
        </div>
      </AppDialog>

      <AppDialog
        open={active === "rec-compare"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="xl"
        icon={<ScaleIcon />}
        title="So sánh 2 lần gợi ý"
        description="Đối chiếu kết quả giữa Lần #12 (15/11) và Lần #14 (28/11)."
        cancelLabel="Đóng"
      >
        <div className="overflow-x-auto rounded-lg border border-line">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Tiêu chí</TableHead>
                <TableHead>Lần #12</TableHead>
                <TableHead>Lần #14</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>IELTS</TableCell>
                <TableCell>7.0</TableCell>
                <TableCell>7.5</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Ngân sách / năm</TableCell>
                <TableCell>$35,000</TableCell>
                <TableCell>$38,000</TableCell>
              </TableRow>
              <TableRow>
                <TableCell>Số trường đạt chuẩn</TableCell>
                <TableCell>8 trường</TableCell>
                <TableCell>12 trường</TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </AppDialog>

      <AppDialog
        open={active === "scen-save"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        title="Lưu kịch bản mô phỏng"
        description="Đặt tên cho kịch bản để so sánh sau."
        cancelLabel="Hủy"
        confirmLabel="Lưu kịch bản"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Saved scenario");
        }}
      >
        <Field>
          <FieldLabel htmlFor="scen-name">Tên kịch bản</FieldLabel>
          <Input id="scen-name" defaultValue="Kịch bản SAT 1480 + Học bổng 30%" />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "scen-list"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="md"
        title="Kịch bản đã lưu (3)"
        description="Chọn một kịch bản để tải lại vào bảng mô phỏng."
        cancelLabel="Đóng"
      >
        <div className="space-y-2 text-sm">
          {["Kịch bản gốc (GPA 8.5)", "Kịch bản SAT 1480", "Kịch bản chuyển tiếp 2+2"].map((name) => (
            <div key={name} className="flex items-center justify-between rounded-xl border border-line p-3">
              <span>{name}</span>
              <Button size="sm" variant="outline" onClick={() => closeActive(`Loaded ${name}`)}>
                Mở
              </Button>
            </div>
          ))}
        </div>
      </AppDialog>

      <AppDialog
        open={active === "scen-del"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa kịch bản đã lưu?"
        description="Kịch bản 'Kịch bản SAT 1480' sẽ bị xóa vĩnh viễn."
        cancelLabel="Hủy"
        confirmLabel="Xóa"
        confirmTone="destructive"
        onConfirm={() => closeActive("Deleted scenario")}
      />

      <AppDialog
        open={active === "ct-form"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<MailIcon />}
        title="Gửi yêu cầu tư vấn tới trung tâm"
        description={MOCK_CENTER.name}
        cancelLabel="Hủy"
        confirmLabel="Gửi yêu cầu"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Submitted contact request");
        }}
      >
        <FieldGroup className="gap-4">
          <Field>
            <FieldLabel htmlFor="ct-msg">Nội dung cần tư vấn</FieldLabel>
            <Textarea id="ct-msg" rows={3} defaultValue="Em muốn tư vấn hồ sơ kỳ Thu 2027." />
          </Field>
        </FieldGroup>
      </AppDialog>

      <AppDialog
        open={active === "ct-age-block"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<ShieldAlertIcon />}
        iconTone="warning"
        title="Cần phụ huynh xác nhận"
        description="Tài khoản dưới 16 tuổi chưa thể tự gửi thông tin liên hệ tới trung tâm tư vấn khi chưa có sự đồng ý của phụ huynh."
        cancelLabel={null}
        confirmLabel="Đã hiểu"
        onConfirm={() => closeActive("Closed age-block view")}
      />

      <AppDialog
        open={active === "ct-success"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<CheckCircle2Icon />}
        iconTone="success"
        title="Đã gửi yêu cầu liên hệ!"
        description="Trung tâm sẽ phản hồi qua hệ thống tin nhắn trong vòng 24 giờ làm việc."
        cancelLabel={null}
        confirmLabel="Hoàn tất"
        onConfirm={() => closeActive("Closed contact success")}
      />

      <AppDialog
        open={active === "rev-remove"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa đánh giá của bạn?"
        description="Đánh giá 5 sao về Trung tâm Minh Anh sẽ bị gỡ khỏi trang công khai."
        cancelLabel="Hủy"
        confirmLabel="Xóa đánh giá"
        confirmTone="destructive"
        onConfirm={() => closeActive("Removed review")}
      />

      <AppDialog
        open={active === "rev-report"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<FlagIcon />}
        iconTone="warning"
        title="Báo cáo đánh giá vi phạm"
        description="Chọn lý do báo cáo để Moderator kiểm tra."
        cancelLabel="Hủy"
        confirmLabel="Gửi báo cáo"
        confirmTone="destructive"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive(`Reported review: ${reviewReportReason}`);
        }}
      >
        <div className="space-y-4">
          <RadioGroup value={reviewReportReason} onValueChange={setReviewReportReason} className="space-y-2">
            {[
              { value: "spam", label: "Spam hoặc quảng cáo" },
              { value: "offensive", label: "Ngôn từ xúc phạm, công kích" },
              { value: "fake", label: "Thông tin sai sự thật / giả mạo" },
            ].map((opt) => (
              <div key={opt.value} className="flex items-center gap-2 rounded-lg border border-line p-2.5">
                <RadioGroupItem value={opt.value} id={`rr-${opt.value}`} />
                <Label htmlFor={`rr-${opt.value}`} className="cursor-pointer text-sm">{opt.label}</Label>
              </div>
            ))}
          </RadioGroup>
          <Textarea rows={2} placeholder="Chi tiết bổ sung (tùy chọn)..." />
        </div>
      </AppDialog>

      <AppDialog
        open={active === "cp-delete"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Yêu cầu xóa hồ sơ trung tâm"
        description="Administrator sẽ xem xét yêu cầu và xử lý theo quy định lưu trữ dữ liệu."
        cancelLabel="Hủy"
        confirmLabel="Gửi yêu cầu xóa"
        confirmTone="destructive"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Submitted center deletion request");
        }}
      >
        <Field>
          <FieldLabel htmlFor="cpd-reason">Lý do yêu cầu xóa</FieldLabel>
          <Textarea id="cpd-reason" rows={3} placeholder="Ví dụ: Trung tâm ngừng hoạt động..." />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "cp-legal-change"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<AlertTriangleIcon />}
        iconTone="warning"
        title="Thay đổi thông tin pháp lý cần duyệt lại?"
        description="Đổi Mã số thuế hoặc Giấy phép đào tạo sẽ chuyển hồ sơ về trạng thái chờ Moderator xác minh lại."
        cancelLabel="Hủy"
        confirmLabel="Lưu và gửi duyệt"
        onConfirm={() => closeActive("Confirmed legal field change")}
      />

      <AppDialog
        open={active === "cp-lead-chat"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="xl"
        icon={<MessageSquareIcon />}
        title="Chi tiết Lead & Trao đổi — Nguyễn Văn Minh"
        description="Mục tiêu: Cử nhân CS · Mỹ · Kỳ Thu 2027"
        cancelLabel="Đóng"
        confirmLabel="Gửi phản hồi"
        onConfirm={() => closeActive("Sent reply to lead")}
      >
        <div className="space-y-3">
          <div className="rounded-xl bg-surface-2 p-3 text-sm">
            Học sinh: &quot;Em cần tư vấn gói sửa bài luận và xin học bổng bang Texas.&quot;
          </div>
          <Textarea rows={3} placeholder="Nhập nội dung phản hồi cho học sinh..." />
        </div>
      </AppDialog>

      <AppDialog
        open={active === "comm-report"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<FlagIcon />}
        iconTone="warning"
        title="Báo cáo tin nhắn cộng đồng"
        description="Tin nhắn vi phạm sẽ được gửi tới Moderator xem xét."
        cancelLabel="Hủy"
        confirmLabel="Báo cáo"
        confirmTone="destructive"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Reported community message");
        }}
      >
        <Field>
          <FieldLabel htmlFor="cm-rep">Lý do</FieldLabel>
          <Textarea id="cm-rep" rows={3} placeholder="Mô tả vi phạm..." />
        </Field>
      </AppDialog>

      <Dialog open={active === "comm-lightbox"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="full" className="bg-[#0f2233] dark:bg-black/95 text-white ring-white/10">
          <DialogHeader>
            <DialogIcon tone="muted" className="text-white/80">
              <ImageIcon />
            </DialogIcon>
            <DialogTitle className="text-white">Xem ảnh đính kèm</DialogTitle>
            <DialogDescription className="text-white/70">
              scholarship-offer-2026.png (1280 × 720)
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="flex items-center justify-center rounded-xl bg-black/40 p-8 text-white/70">
            <div className="flex h-56 w-full max-w-lg items-center justify-center rounded-lg border border-white/15 bg-white/5 text-sm">
              [Mock Image Preview Surface]
            </div>
          </DialogBody>
          <DialogFooter layout="single">
            <DialogCancel tone="tint">Đóng</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ==================== 5. ADMIN & MODERATOR ==================== */}
      <AppDialog
        open={active === "adm-lock"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<LockIcon />}
        iconTone="destructive"
        title="Khóa tài khoản người dùng"
        description="Người dùng sẽ bị đăng xuất ngay lập tức và không thể đăng nhập."
        cancelLabel="Hủy"
        confirmLabel="Khóa tài khoản"
        confirmTone="destructive"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Locked user");
        }}
      >
        <Field>
          <FieldLabel htmlFor="adm-lock-reason">Lý do khóa</FieldLabel>
          <Textarea id="adm-lock-reason" rows={2} placeholder="Vi phạm điều khoản cộng đồng..." />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "adm-unlock"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<UnlockIcon />}
        iconTone="success"
        title="Mở khóa tài khoản?"
        description="Người dùng minh.nguyen@example.com sẽ được khôi phục quyền truy cập."
        cancelLabel="Hủy"
        confirmLabel="Mở khóa"
        confirmTone="ok"
        onConfirm={() => closeActive("Unlocked user (tone='ok')")}
      />

      <AppDialog
        open={active === "adm-reset-2fa"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<KeyRoundIcon />}
        iconTone="warning"
        title="Đặt lại xác thực 2 bước (2FA)?"
        description="Moderator sẽ cần quét lại mã QR mới ở lần đăng nhập kế tiếp."
        cancelLabel="Hủy"
        confirmLabel="Đặt lại 2FA"
        confirmTone="destructive"
        onConfirm={() => closeActive("Reset 2FA")}
      />

      <AppDialog
        open={active === "adm-log-detail"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="lg"
        title="Chi tiết nhật ký truy cập #LOG-8921"
        description="28/11/2026 18:10:04 · POST /api/admin/centers/verify"
        cancelLabel="Đóng"
      >
        <div className="space-y-3 text-sm">
          <div className="grid grid-cols-2 gap-2 rounded-xl bg-surface-2 p-3">
            <div><strong>Actor:</strong> admin@usas.vn</div>
            <div><strong>Status:</strong> 200 OK</div>
            <div><strong>IP:</strong> 14.232.110.42</div>
            <div><strong>Duration:</strong> 84 ms</div>
          </div>
          <Button type="button" size="sm" variant="outline" onClick={() => setJsonExpanded((v) => !v)}>
            {jsonExpanded ? "Ẩn JSON payload" : "Mở rộng JSON payload"}
          </Button>
          {jsonExpanded && (
            <pre className="overflow-x-auto rounded-lg border border-line bg-surface-2 p-3 text-xs">
              {JSON.stringify({ centerId: MOCK_CENTER.id, action: "verify", note: "OK" }, null, 2)}
            </pre>
          )}
        </div>
      </AppDialog>

      <AppDialog
        open={active === "adm-verif-queue"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="2xl"
        icon={<ClipboardCheckIcon />}
        title="Hàng đợi xác minh trung tâm (3)"
        description="Các hồ sơ trung tâm đang chờ Moderator đối chiếu pháp lý."
        cancelLabel="Đóng"
      >
        <div className="overflow-x-auto rounded-lg border border-line">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Mã</TableHead>
                <TableHead>Tên trung tâm</TableHead>
                <TableHead>Thành phố</TableHead>
                <TableHead>Trạng thái</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell>USAS-HN-01</TableCell>
                <TableCell>Trung tâm Minh Anh</TableCell>
                <TableCell>Hà Nội</TableCell>
                <TableCell><Badge variant="secondary">Chờ duyệt</Badge></TableCell>
              </TableRow>
              <TableRow>
                <TableCell>USAS-HCM-04</TableCell>
                <TableCell>Global Pathway Edu</TableCell>
                <TableCell>TP.HCM</TableCell>
                <TableCell><Badge variant="secondary">Chờ duyệt</Badge></TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      </AppDialog>

      <Dialog open={active === "adm-verif-review"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="form" size="xl">
          <DialogHeader>
            <DialogIcon tone="brand">
              <ShieldCheckIcon />
            </DialogIcon>
            <DialogTitle>Đối chiếu hồ sơ — {MOCK_CENTER.name}</DialogTitle>
            <DialogDescription>
              Kiểm tra MST {MOCK_CENTER.taxCode} và Giấy phép {MOCK_CENTER.licenseNumber}.
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="space-y-3 text-sm">
            <div className="rounded-xl border border-line p-3 space-y-1">
              <p><strong>Người đại diện:</strong> {MOCK_CENTER.legalRepresentative}</p>
              <p><strong>Địa chỉ:</strong> {MOCK_CENTER.address}, {MOCK_CENTER.city}</p>
            </div>
            <Textarea rows={2} placeholder="Ghi chú đối chiếu hồ sơ..." />
          </DialogBody>
          <DialogFooter layout="split-top">
            <DialogAction tone="primary-tint" onClick={() => notify("Opened legal documents preview")}>
              Mở tài liệu đính kèm
            </DialogAction>
            <DialogCancel tone="tint">Đóng</DialogCancel>
            <DialogAction tone="ok" onClick={() => closeActive("Approved verification")}>
              Xác minh trung tâm
            </DialogAction>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <CenterActionConfirmDialog
        center={centerAction ? MOCK_CENTER : null}
        action={centerAction}
        isSubmitting={false}
        onClose={() => setCenterAction(null)}
        onConfirm={(note) => {
          notify(`CenterActionConfirmDialog (${centerAction}) confirmed${note ? `: ${note}` : ""}`);
          setCenterAction(null);
        }}
      />

      <AppDialog
        open={active === "adm-cpc-fail"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        icon={<BanIcon />}
        iconTone="destructive"
        title="Đánh dấu kiểm tra thất bại & Ẩn trung tâm"
        description="Fanpage hoặc thông tin cập nhật không khớp với hồ sơ pháp lý."
        cancelLabel="Hủy"
        confirmLabel="Đánh dấu thất bại & Ẩn"
        confirmTone="destructive"
        onSubmit={(e) => {
          e.preventDefault();
          closeActive("Marked profile change failed & hid center");
        }}
      >
        <Field>
          <FieldLabel htmlFor="cpc-note">Lý do từ chối</FieldLabel>
          <Textarea id="cpc-note" rows={2} placeholder="Liên kết fanpage không thuộc sở hữu trung tâm..." />
        </Field>
      </AppDialog>

      <AppDialog
        open={active === "adm-crr-action"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<FlagIcon />}
        iconTone="destructive"
        title="Xử lý báo cáo đánh giá"
        description="Gỡ bỏ đánh giá vi phạm hoặc bác bỏ báo cáo này."
        cancelLabel="Bác bỏ báo cáo"
        confirmLabel="Gỡ đánh giá"
        confirmTone="destructive"
        onCancel={() => notify("Dismissed review report")}
        onConfirm={() => closeActive("Removed reported review")}
      />

      <Dialog open={active === "adm-cdr-action"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="action" size="sm">
          <DialogHeader>
            <DialogIcon tone="warning">
              <Building2Icon />
            </DialogIcon>
            <DialogTitle>Xử lý yêu cầu xóa trung tâm</DialogTitle>
            <DialogDescription>{MOCK_CENTER.name}</DialogDescription>
          </DialogHeader>
          <DialogFooter layout="stack">
            <DialogAction tone="destructive" onClick={() => closeActive("Approved center deletion")}>
              Duyệt xóa (30 ngày)
            </DialogAction>
            <DialogAction tone="primary-tint" onClick={() => closeActive("Extended center deletion")}>
              Gia hạn thêm thời gian
            </DialogAction>
            <DialogAction tone="tint" onClick={() => closeActive("Rejected center deletion")}>
              Từ chối yêu cầu
            </DialogAction>
            <DialogCancel tone="ghost">Đóng</DialogCancel>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <CenterLegalHoldDialog
        target={
          legalHoldAction
            ? { action: legalHoldAction, centerId: MOCK_CENTER.id, centerName: MOCK_CENTER.name }
            : null
        }
        onClose={() => setLegalHoldAction(null)}
        onDone={() => setLegalHoldAction(null)}
      />

      <Dialog open={active === "adm-birth-review"} onOpenChange={(o) => !o && closeActive()}>
        <DialogContent variant="form" size="lg">
          <DialogHeader>
            <DialogIcon tone="brand">
              <ClipboardCheckIcon />
            </DialogIcon>
            <DialogTitle>Duyệt yêu cầu sửa ngày sinh</DialogTitle>
            <DialogDescription>
              Học sinh: {MOCK_USER.fullName} · Ngày sinh cũ: 2008-05-14 → Mới: 2006-05-14
            </DialogDescription>
          </DialogHeader>
          <DialogBody className="space-y-3 text-sm">
            <div className="rounded-xl border border-line bg-surface-2 p-3">
              <strong>Lý do:</strong> Nhập nhầm năm sinh khi tạo tài khoản. (Có đính kèm CCCD)
            </div>
            {birthReviewRejecting && (
              <Field>
                <FieldLabel htmlFor="brr-reason">Lý do từ chối</FieldLabel>
                <Textarea
                  id="brr-reason"
                  rows={2}
                  value={birthReviewRejectReason}
                  onChange={(e) => setBirthReviewRejectReason(e.target.value)}
                  placeholder="Ảnh mờ, không rõ ngày sinh..."
                />
              </Field>
            )}
          </DialogBody>
          <DialogFooter layout="split-top">
            <DialogAction
              tone="destructive-tint"
              onClick={() => setBirthReviewRejecting((v) => !v)}
            >
              {birthReviewRejecting ? "Ẩn lý do từ chối" : "Từ chối yêu cầu"}
            </DialogAction>
            <DialogCancel tone="tint">Đóng</DialogCancel>
            {birthReviewRejecting ? (
              <DialogAction
                tone="destructive"
                disabled={!birthReviewRejectReason.trim()}
                onClick={() => closeActive("Rejected birth date request")}
              >
                Xác nhận từ chối
              </DialogAction>
            ) : (
              <DialogAction tone="ok" onClick={() => closeActive("Approved birth date request")}>
                Duyệt ngày sinh
              </DialogAction>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AppDialog
        open={active === "adm-lead-mod"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="lg"
        icon={<ShieldAlertIcon />}
        iconTone="warning"
        title="Kiểm duyệt báo cáo tin nhắn Lead"
        description="Ngữ cảnh hội thoại giữa Học sinh và Trung tâm."
        cancelLabel="Bác bỏ báo cáo"
        confirmLabel="Ẩn tin nhắn & Cảnh cáo"
        confirmTone="destructive"
        onConfirm={() => closeActive("Moderated lead message")}
      >
        <div className="space-y-3 text-sm">
          <div className="rounded-xl bg-surface-2 p-3">
            <strong>Tin nhắn bị báo cáo:</strong> &quot;Chuyển khoản đặt cọc ngay qua tài khoản cá nhân để giữ suất.&quot;
          </div>
          <Textarea rows={2} placeholder="Ghi chú xử lý của Moderator..." />
        </div>
      </AppDialog>

      <AppDialog
        open={active === "adm-billing-plan"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<CoinsIcon />}
        iconTone="destructive"
        title="Xóa gói cước / Xử lý đơn hàng?"
        description="Xác nhận xóa gói 'Premium Tháng' hoặc kích hoạt/từ chối đơn #ORD-4402."
        cancelLabel="Từ chối đơn"
        confirmLabel="Xóa gói / Kích hoạt"
        confirmTone="destructive"
        onConfirm={() => closeActive("Billing action confirmed")}
      />

      <AppDialog
        open={active === "adm-grade-save"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<CheckCircle2Icon />}
        iconTone="warning"
        title="Lưu cấu hình thang điểm mới?"
        description="Thay đổi quy đổi GPA sẽ áp dụng cho tất cả lượt đánh giá hồ sơ tiếp theo."
        cancelLabel="Hủy"
        confirmLabel="Lưu cấu hình"
        onConfirm={() => closeActive("Saved grade scale")}
      />

      <AppDialog
        open={active === "adm-grade-hist"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="lg"
        icon={<HistoryIcon />}
        title="Lịch sử thay đổi thang điểm"
        description="Các phiên bản quy đổi GPA đã lưu."
        cancelLabel="Đóng"
      >
        <div className="space-y-2 text-sm">
          <div className="rounded-xl border border-line p-3">
            <strong>v4 (Hiện tại)</strong> · 20/11/2026 · Cập nhật mốc 8.5 → 3.5/4.0
          </div>
          <div className="rounded-xl border border-line p-3">
            <strong>v3</strong> · 02/10/2026 · Khởi tạo thang điểm 10 Việt Nam
          </div>
        </div>
      </AppDialog>

      <AppDialog
        open={active === "adm-policy-pub"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<FileTextIcon />}
        iconTone="warning"
        title="Xuất bản bản nháp chính sách v2.5?"
        description="Người dùng sẽ được yêu cầu đồng ý lại ở lần đăng nhập kế tiếp."
        cancelLabel="Hủy bản nháp"
        confirmLabel="Xuất bản ngay"
        onConfirm={() => closeActive("Published policy draft")}
      />

      <AppDialog
        open={active === "adm-policy-read"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="xl"
        icon={<BookOpenIcon />}
        title="Chính sách bảo mật dữ liệu (v2.4)"
        description="Ban hành: 01/11/2026 · Hiệu lực: Đang áp dụng"
        cancelLabel="Đóng"
      >
        <div className="space-y-3 text-sm leading-relaxed text-ink-2">
          <p>1. Phạm vi thu thập: Thông tin học thuật, điểm số và nguyện vọng du học do học sinh hoặc phụ huynh cung cấp.</p>
          <p>2. Quyền rút đồng ý: Phụ huynh có thể rút đồng ý xử lý dữ liệu bất kỳ lúc nào trong phần Tài khoản.</p>
          <p>3. Thời hạn lưu trữ: Tài khoản chờ xóa được giữ 30 ngày để khôi phục trước khi xóa vĩnh viễn.</p>
        </div>
      </AppDialog>

      <AppDialog
        open={active === "adm-kb-del"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<Trash2Icon />}
        iconTone="destructive"
        title="Xóa mục kho tri thức?"
        description="Mục KB #104 sẽ bị ẩn khỏi trợ lý AI sau khi xóa."
        cancelLabel="Hủy"
        confirmLabel="Xóa mục"
        confirmTone="destructive"
        onConfirm={() => closeActive("Deleted KB/SLM entry")}
      />

      <AppDialog
        open={active === "adm-kb-restore"}
        onOpenChange={(o) => !o && closeActive()}
        variant="action"
        icon={<RotateCcwIcon />}
        iconTone="brand"
        title="Khôi phục phiên bản #3?"
        description="Nội dung hiện tại sẽ được thay thế bằng bản lưu lúc 12/11/2026."
        cancelLabel="Hủy"
        confirmLabel="Khôi phục"
        onConfirm={() => closeActive("Restored version #3")}
      />

      <AppDialog
        open={active === "adm-kb-import"}
        onOpenChange={(o) => !o && closeActive()}
        variant="form"
        size="full"
        icon={<FileSpreadsheetIcon />}
        title="Nhập dữ liệu từ Excel (.xlsx)"
        description="Xem trước 60 dòng trước khi lưu vào hệ thống."
        cancelLabel="Hủy"
        confirmLabel="Xác nhận nhập"
        onConfirm={() => closeActive("Imported Excel preview")}
      >
        <div className="space-y-3">
          <FieldDescription>File mẫu: knowledge_batch_2026_11.xlsx (2 sheets, 0 lỗi nghiêm trọng)</FieldDescription>
          <div className="overflow-x-auto rounded-lg border border-line">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Dòng</TableHead>
                  <TableHead>Loại</TableHead>
                  <TableHead>Tiêu đề / Câu hỏi</TableHead>
                  <TableHead>Topic</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow>
                  <TableCell>2</TableCell>
                  <TableCell>faq</TableCell>
                  <TableCell>Điều kiện xin visa F-1 cần chứng minh tài chính bao nhiêu?</TableCell>
                  <TableCell>VISA_F1</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell>3</TableCell>
                  <TableCell>timeline</TableCell>
                  <TableCell>Lộ trình nộp hồ sơ kỳ Thu (Fall Intake)</TableCell>
                  <TableCell>TIMELINE_FALL</TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </div>
      </AppDialog>

      <KnowledgeImportResultDialog
        result={kbImportResult}
        onClose={() => setKbImportResult(null)}
      />

      <SlmImportResultDialog
        result={slmImportResult}
        onClose={() => setSlmImportResult(null)}
      />
    </div>
  );
}
