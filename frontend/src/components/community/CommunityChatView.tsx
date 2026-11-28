"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowDownIcon,
  Building2Icon,
  ChevronUpIcon,
  CornerUpLeftIcon,
  EyeOffIcon,
  FlagIcon,
  GraduationCapIcon,
  ArrowUpIcon,
  HeartHandshakeIcon,
  LockIcon,
  PlusIcon,
  ScrollTextIcon,
  ShieldCheckIcon,
  ShieldIcon,
  SmileIcon,
  UsersIcon,
  XIcon,
  type LucideIcon,
} from "lucide-react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
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
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Skeleton } from "@/components/ui/skeleton";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { useAuth } from "@/contexts/AuthContext";
import { useCommunityChat } from "@/hooks/use-community-chat";
import { ApiError } from "@/lib/api";
import { adminForumApi, communityApi } from "@/lib/community-api";
import { setHeaderHidden, useHeaderHidden } from "@/lib/header-visibility";
import { formatUserRole } from "@/lib/user-role-labels";
import { cn } from "@/lib/utils";
import type { UserRole } from "@/types/api";
import type { CommunityMessageDto, CommunityPresenceDto, CommunityRestrictionDto } from "@/types/community";

const EmojiPicker = React.lazy(() => import("emoji-picker-react"));

const MAX_LEN = 2000;
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];
const ONLINE_WINDOW_MS = 5 * 60 * 1000;

interface RoleMeta {
  icon: LucideIcon | null;
  iconClass?: string;
  /** Màu tên / biểu tượng vai trò */
  text: string;
  /** Nền + chữ của avatar chữ cái đầu */
  avatar: string;
}

const ROLE_META: Record<UserRole, RoleMeta> = {
  student: {
    icon: GraduationCapIcon,
    text: "text-brand",
    avatar: "bg-brand-wash text-brand",
  },
  parent: {
    icon: HeartHandshakeIcon,
    text: "text-ok",
    avatar: "bg-ok-wash text-ok",
  },
  center: {
    icon: Building2Icon,
    text: "text-gold",
    avatar: "bg-gold-wash text-gold",
  },
  moderator: {
    icon: ShieldCheckIcon,
    text: "text-warn",
    avatar: "bg-warn-wash text-warn",
  },
  admin: {
    icon: ShieldIcon,
    iconClass: "fill-current/15",
    text: "text-ink",
    avatar: "bg-ground text-ink",
  },
};
const UNKNOWN_ROLE_META: RoleMeta = {
  icon: null,
  text: "text-ink",
  avatar: "bg-ground text-ink",
};

/** Thứ tự nhóm trong bảng thành viên: nhân sự trước, giống Discord */
const ROLE_SECTION_ORDER: (UserRole | "")[] = ["moderator", "admin", "center", "parent", "student", ""];

function roleMeta(role: string): RoleMeta {
  return ROLE_META[role as UserRole] ?? UNKNOWN_ROLE_META;
}

function roleSectionLabel(role: string) {
  return role ? formatUserRole(role as UserRole) : "Thành viên";
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Giờ dạng "17:00" */
function formatHm(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
}

function formatFullDateTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("vi-VN", {
    hour: "2-digit",
    minute: "2-digit",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

function getDayKey(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`;
}

function formatDaySeparator(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const now = new Date();
  const todayKey = `${now.getFullYear()}-${now.getMonth() + 1}-${now.getDate()}`;
  const dKey = `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`;

  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const yesterdayKey = `${yesterday.getFullYear()}-${yesterday.getMonth() + 1}-${yesterday.getDate()}`;

  if (dKey === todayKey) return "Hôm nay";
  if (dKey === yesterdayKey) return "Hôm qua";
  return d.toLocaleDateString("vi-VN", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

/** "Hôm nay 17:00" / "Hôm qua 17:00" / "dd/MM/yyyy 17:00" */
function formatHeaderTime(iso: string) {
  const day = formatDaySeparator(iso);
  const hm = formatHm(iso);
  return day && hm ? `${day} ${hm}` : day || hm;
}

/** Tự biến link URL trong văn bản thành thẻ <a> bấm được, loại trừ dấu câu cuối link */
function renderMessageContent(content: string) {
  if (!content) return null;
  const urlRegex = /(https?:\/\/[^\s]+)/g;
  const parts = content.split(urlRegex);
  return parts.map((part, index) => {
    if (part.match(urlRegex)) {
      const trailingMatch = part.match(/[.,;:!?)]+$/);
      const trailing = trailingMatch ? trailingMatch[0] : "";
      const cleanUrl = trailing ? part.slice(0, -trailing.length) : part;

      return (
        <React.Fragment key={index}>
          <a
            href={cleanUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="text-brand underline underline-offset-2 hover:text-brand-dark break-all"
          >
            {cleanUrl}
          </a>
          {trailing}
        </React.Fragment>
      );
    }
    return <React.Fragment key={index}>{part}</React.Fragment>;
  });
}

/** Gắn Tooltip vào một nút/phần tử đã dựng sẵn (truyền nguyên phần tử, kể cả Sheet/PopoverTrigger) */
function Tip({ label, children }: { label: string; children: React.ReactElement }) {
  return (
    <Tooltip>
      <TooltipTrigger render={children} />
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  );
}

function RoleIcon({ role }: { role: string }) {
  const meta = roleMeta(role);
  const Icon = meta.icon;
  if (!Icon) return null;
  const label = formatUserRole(role as UserRole);
  return (
    <Tooltip>
      <TooltipTrigger render={<span className="inline-flex shrink-0 cursor-default items-center" />}>
        <Icon aria-hidden className={cn("size-3.5", meta.text, meta.iconClass)} />
        <span className="sr-only">{label}</span>
      </TooltipTrigger>
      <TooltipContent>{label}</TooltipContent>
    </Tooltip>
  );
}

/** Nút trong thanh kênh: nền trong suốt cả khi bật; trạng thái bật chỉ thể hiện bằng icon sáng hơn */
const TOOLBAR_BTN =
  "size-8 bg-transparent text-brand-foreground/60 hover:bg-white/10 hover:text-brand-foreground aria-pressed:bg-transparent aria-pressed:text-brand-foreground aria-expanded:bg-transparent aria-expanded:text-brand-foreground";

/** Cột bên (nội quy, thành viên) trên lg: trượt mở/thu bằng bề rộng; nội dung giữ w-72 cố định để chữ không bị bóp khi chạy hiệu ứng.
 *  Khi thu: rộng 0 và -ml-2 để bù khoảng gap-2 của hàng cha. */
const SIDE_PANEL =
  "hidden min-h-0 shrink-0 overflow-hidden transition-[width,margin-left,opacity] duration-300 ease-[cubic-bezier(0.22,1,0.36,1)] motion-reduce:transition-none lg:block";
const SIDE_OPEN = "w-72 opacity-100";
const SIDE_CLOSED = "-ml-2 w-0 opacity-0";

/** Nội quy diễn đàn cố định cho kênh chung */
const FORUM_RULES: { title: string; desc: string }[] = [
  {
    title: "Tôn trọng mọi người",
    desc: "Lịch sự với học sinh, phụ huynh và trung tâm. Không xúc phạm, miệt thị, phân biệt hay công kích cá nhân.",
  },
  {
    title: "Không quảng cáo",
    desc: "Không chào mời dịch vụ, bán khóa học, đăng link tiếp thị hay rủ nhắn riêng để tư vấn. Quy định này áp dụng cả với trung tâm.",
  },
  {
    title: "Giữ kín thông tin cá nhân",
    desc: "Không đăng số điện thoại, địa chỉ, giấy tờ tùy thân hay hồ sơ của mình hoặc người khác.",
  },
  {
    title: "Chia sẻ trung thực",
    desc: "Nói đúng trải nghiệm của bạn. Khi nêu học phí, học bổng hay hạn nộp, hãy ghi rõ nguồn và thời điểm.",
  },
  {
    title: "Không gian lận hồ sơ",
    desc: "Không mua bán, làm giả hay viết thuê bài luận, bảng điểm, chứng chỉ và giấy tờ tài chính.",
  },
  {
    title: "Đúng chủ đề",
    desc: "Giữ cuộc trò chuyện quanh du học: chọn trường, hồ sơ, chi phí, học bổng và cuộc sống du học.",
  },
  {
    title: "Báo cáo thay vì tranh cãi",
    desc: "Gặp tin vi phạm, hãy bấm Báo cáo. Kiểm duyệt viên có thể ẩn tin và hạn chế quyền chat của tài khoản vi phạm.",
  },
];

/** Dấu số thứ tự của nội quy: lần lượt hình thoi, vuông bo góc, tròn */
/** Màu từng số nội quy: nền nhạt + viền + số cùng tông, xoay vòng */
const RULE_TONES = [
  { fill: "bg-brand-wash border-brand/40", text: "text-brand" },
  { fill: "bg-ok-wash border-ok/40", text: "text-ok" },
  { fill: "bg-gold-wash border-gold/40", text: "text-gold" },
  { fill: "bg-warn-wash border-warn/40", text: "text-warn" },
  { fill: "bg-reach-wash border-reach/40", text: "text-reach" },
];

function RuleMarker({ n }: { n: number }) {
  const shape = (n - 1) % 3;
  const tone = RULE_TONES[(n - 1) % RULE_TONES.length];
  return (
    <span className={cn("relative flex size-8 shrink-0 items-center justify-center text-sm font-semibold", tone.text)}>
      <span
        aria-hidden
        className={cn(
          "absolute border-[1.5px]",
          tone.fill,
          shape === 0 && "size-[22px] rotate-45 rounded-[5px]",
          shape === 1 && "size-7 rounded-lg",
          shape === 2 && "size-7 rounded-full",
        )}
      />
      <span className="relative tabular-nums">{n}</span>
    </span>
  );
}

function RulesList() {
  return (
    <ol className="min-h-0 flex-1 space-y-4 overflow-y-auto px-4 py-4">
      {FORUM_RULES.map((rule, i) => (
        <li key={rule.title} className="flex gap-3">
          <RuleMarker n={i + 1} />
          <div className="min-w-0 pt-0.5">
            <p className="text-sm font-semibold text-ink">{rule.title}</p>
            <p className="mt-0.5 text-[13px] leading-relaxed text-ink-2">{rule.desc}</p>
          </div>
        </li>
      ))}
    </ol>
  );
}

function MemberPanel({
  members,
  currentUserId,
  className,
}: {
  members: CommunityPresenceDto[];
  currentUserId?: string;
  className?: string;
}) {
  const [nowMs, setNowMs] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNowMs(Date.now()), 60_000);
    return () => clearInterval(timer);
  }, []);

  const sections = useMemo(() => {
    const groups = new Map<string, CommunityPresenceDto[]>();
    for (const m of members) {
      const key = ROLE_SECTION_ORDER.includes(m.role) ? m.role : "";
      groups.set(key, [...(groups.get(key) ?? []), m]);
    }
    return ROLE_SECTION_ORDER.filter((r) => groups.has(r)).map((r) => ({
      role: r,
      items: groups.get(r)!,
    }));
  }, [members]);

  if (members.length === 0 || (members.length === 1 && members[0].userId === currentUserId)) {
    return (
      <div className={cn("flex flex-1 flex-col items-center justify-center gap-2 px-4 py-8 text-center", className)}>
        <div className="flex size-10 items-center justify-center rounded-full bg-white/10 text-white/70">
          <UsersIcon className="size-5" aria-hidden />
        </div>
        <p className="text-sm text-white/70">
          {members.length === 0 ? "Chưa có ai trong phòng." : "Ở đây yên tĩnh quá.. vui lòng chờ một lúc nhé"}
        </p>
      </div>
    );
  }

  return (
    <div className={cn("min-h-0 flex-1 space-y-5 overflow-y-auto px-2 py-4", className)}>
      {sections.map(({ role, items }) => {
        const meta = roleMeta(role);
        return (
          <section key={role || "other"}>
            <h2 className="flex items-baseline justify-between gap-2 px-2 pb-1.5 font-heading text-sm font-semibold text-white/85">
              <span>{roleSectionLabel(role)}</span>
              <span className="font-sans text-xs font-medium tabular-nums text-white/50">{items.length}</span>
            </h2>
            <ul>
              {items.map((m) => {
                const isSelf = m.userId === currentUserId;
                const lastSeen = m.lastSeenAt ? new Date(m.lastSeenAt) : null;
                let lastSeenText = "Đang online";
                let online = true;
                if (lastSeen && !Number.isNaN(lastSeen.getTime())) {
                  const diffMs = nowMs - lastSeen.getTime();
                  online = diffMs < ONLINE_WINDOW_MS;
                  if (diffMs < 2 * 60 * 1000) {
                    lastSeenText = "Vừa xong";
                  } else if (diffMs < 60 * 60 * 1000) {
                    lastSeenText = `${Math.floor(diffMs / (60 * 1000))} phút trước`;
                  } else {
                    lastSeenText = lastSeen.toLocaleTimeString("vi-VN", {
                      hour: "2-digit",
                      minute: "2-digit",
                    });
                  }
                }

                return (
                  <li
                    key={m.userId}
                    title={lastSeenText}
                    className="flex items-center gap-3 rounded-md px-2 py-1.5 hover:bg-white/10"
                  >
                    <div className="relative shrink-0">
                      <Avatar>
                        <AvatarFallback className={cn("text-xs font-semibold", meta.avatar)}>
                          {initials(m.fullName)}
                        </AvatarFallback>
                      </Avatar>
                      <span
                        className={cn(
                          "absolute -right-0.5 -bottom-0.5 size-3 rounded-full ring-2 ring-brand-deep dark:ring-black",
                          online ? "bg-ok" : "bg-warn",
                        )}
                        aria-hidden
                      />
                    </div>
                    <p className="min-w-0 flex-1 truncate text-sm text-white/90">
                      {m.fullName}
                      {isSelf ? <span className="ml-1.5 text-xs text-white/50">bạn</span> : null}
                      <span className="sr-only">{online ? ", đang online" : ", vắng mặt"}</span>
                    </p>
                  </li>
                );
              })}
            </ul>
          </section>
        );
      })}
    </div>
  );
}

export function CommunityChatView() {
  const { user } = useAuth();
  const [messages, setMessages] = useState<CommunityMessageDto[]>([]);
  const [members, setMembers] = useState<CommunityPresenceDto[]>([]);
  const [restriction, setRestriction] = useState<CommunityRestrictionDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [reported, setReported] = useState<Set<string>>(() => new Set());
  const [hidingId, setHidingId] = useState<string | null>(null);
  const [confirmingHideId, setConfirmingHideId] = useState<string | null>(null);
  const [newMessagesCount, setNewMessagesCount] = useState(0);

  // Trả lời tin nhắn
  const [replyingTo, setReplyingTo] = useState<CommunityMessageDto | null>(null);

  // Đính kèm ảnh
  const [selectedImage, setSelectedImage] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  // Lightbox xem ảnh lớn
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);

  // Emoji Popover
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);

  // Popup Báo cáo tin nhắn
  const [reportTarget, setReportTarget] = useState<CommunityMessageDto | null>(null);
  const [reportReason, setReportReason] = useState("");
  const [reportDetail, setReportDetail] = useState("");
  const [submittingReport, setSubmittingReport] = useState(false);

  const listRef = useRef<HTMLDivElement | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const isNearBottomRef = useRef(true);
  const initialLoadedRef = useRef(false);
  const messagesRef = useRef<CommunityMessageDto[]>([]);

  useEffect(() => {
    messagesRef.current = messages;
  }, [messages]);

  const isStaff = user?.role === "admin" || user?.role === "moderator";

  const scrollToBottom = useCallback((smooth = false) => {
    const el = listRef.current;
    if (!el) return;
    if (smooth) {
      el.scrollTo({ top: el.scrollHeight, behavior: "smooth" });
    } else {
      el.scrollTop = el.scrollHeight;
    }
  }, []);

  const mergeMessages = useCallback(
    (incoming: CommunityMessageDto[], options?: { isOlder?: boolean; isMine?: boolean }) => {
      const knownIds = new Set(messagesRef.current.map((m) => m.id));
      const freshFromOthers = incoming.filter((m) => !knownIds.has(m.id) && m.userId !== user?.id).length;
      setMessages((prev) => {
        const map = new Map(prev.map((m) => [m.id, m]));
        for (const item of incoming) {
          const mine = item.userId === user?.id;
          map.set(item.id, { ...item, isMine: mine || item.isMine });
        }
        return [...map.values()].sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id));
      });

      if (options?.isMine) {
        setNewMessagesCount(0);
        requestAnimationFrame(() => scrollToBottom(true));
      } else if (!options?.isOlder) {
        if (isNearBottomRef.current) {
          requestAnimationFrame(() => scrollToBottom(true));
        } else if (freshFromOthers > 0) {
          setNewMessagesCount((prev) => prev + freshFromOthers);
        }
      }
    },
    [user?.id, scrollToBottom],
  );

  const loadInitial = useCallback(async () => {
    const batch = await communityApi.listMessages({ take: 50 });
    const chronological = [...batch].reverse();
    mergeMessages(chronological);
    setHasMore(batch.length >= 50);
  }, [mergeMessages]);

  useEffect(() => {
    if (loading || initialLoadedRef.current) return;
    initialLoadedRef.current = true;
    scrollToBottom(false);
  }, [loading, scrollToBottom]);

  const loadOlder = useCallback(async () => {
    const oldest = messages[0];
    if (!oldest || loadingOlder || !hasMore) return;
    const el = listRef.current;
    const prevScrollHeight = el?.scrollHeight ?? 0;
    const prevScrollTop = el?.scrollTop ?? 0;

    setLoadingOlder(true);
    try {
      const batch = await communityApi.listMessages({
        before: oldest.createdAt,
        beforeId: oldest.id,
        take: 50,
      });
      const chronological = [...batch].reverse();
      mergeMessages(chronological, { isOlder: true });
      setHasMore(batch.length >= 50);

      requestAnimationFrame(() => {
        if (el) {
          const heightDiff = el.scrollHeight - prevScrollHeight;
          el.scrollTop = prevScrollTop + heightDiff;
        }
      });
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không tải được tin cũ",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setLoadingOlder(false);
    }
  }, [messages, loadingOlder, hasMore, mergeMessages]);

  const handleScroll = () => {
    const el = listRef.current;
    if (!el) return;
    const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    isNearBottomRef.current = distanceFromBottom < 100;

    if (isNearBottomRef.current && newMessagesCount > 0) {
      setNewMessagesCount(0);
    }

    if (el.scrollTop < 60 && !loadingOlder && hasMore) {
      void loadOlder();
    }
  };

  const resync = useCallback(async () => {
    try {
      const currentList = messagesRef.current;
      const latest = currentList[currentList.length - 1];
      if (latest) {
        const newMessages: CommunityMessageDto[] = [];
        let lastTime = latest.createdAt;
        let lastId: string | undefined = latest.id;
        let fetchedTotal = 0;

        while (fetchedTotal < 200) {
          const batch = await communityApi.listMessages({
            after: lastTime,
            afterId: lastId,
            take: 50,
          });
          if (batch.length === 0) break;
          newMessages.push(...batch);
          fetchedTotal += batch.length;
          lastTime = batch[batch.length - 1].createdAt;
          lastId = batch[batch.length - 1].id;
          if (batch.length < 50) break;
        }

        if (fetchedTotal >= 200) {
          const latestBatch = await communityApi.listMessages({ take: 50 });
          setMessages(
            [...latestBatch].reverse().map((m) => ({
              ...m,
              isMine: m.userId === user?.id || m.isMine,
            })),
          );
          setHasMore(latestBatch.length >= 50);
          setNewMessagesCount(0);
          requestAnimationFrame(() => scrollToBottom(false));
        } else if (newMessages.length > 0) {
          mergeMessages(newMessages);
        }
      } else {
        await loadInitial();
      }
      const r = await communityApi.restriction();
      setRestriction(r.isRestricted ? r : null);
    } catch {
      // Background resync error ignored
    }
  }, [mergeMessages, loadInitial, user?.id, scrollToBottom]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        await loadInitial();
        const r = await communityApi.restriction();
        if (!cancelled) setRestriction(r.isRestricted ? r : null);
      } catch (err) {
        if (!cancelled) {
          toast.add({
            type: "error",
            title: "Không tải được phòng chat",
            description: err instanceof ApiError ? err.message : "Thử lại sau.",
          });
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [loadInitial]);

  useCommunityChat({
    onNewMessage: (message) => mergeMessages([message]),
    onHidden: (id) => setMessages((prev) => prev.filter((m) => m.id !== id)),
    onResync: resync,
    onMembers: setMembers,
    onMemberChange: (joined, member) => {
      setMembers((prev) => {
        const without = prev.filter((m) => m.userId !== member.userId);
        return joined ? [...without, member].sort((a, b) => a.fullName.localeCompare(b.fullName, "vi")) : without;
      });
    },
    onRestrictionChanged: (data) => {
      setRestriction(data.isRestricted ? data : null);
      if (data.isRestricted) {
        toast.add({
          type: "warn",
          title: "Thông báo hạn chế chat",
          description: data.reason ? `Lý do: ${data.reason}` : "Bạn đã bị hạn chế chat.",
        });
      } else {
        toast.add({
          type: "success",
          title: "Đã gỡ hạn chế chat",
          description: "Bạn có thể gửi tin nhắn bình thường.",
        });
      }
    },
  });

  const handleStartReply = (msg: CommunityMessageDto) => {
    setReplyingTo(msg);
    textareaRef.current?.focus();
  };

  const handleCancelReply = () => {
    setReplyingTo(null);
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      toast.add({
        type: "error",
        title: "Định dạng không hỗ trợ",
        description: "Chỉ nhận ảnh JPG, PNG hoặc WebP.",
      });
      return;
    }

    if (file.size > MAX_IMAGE_BYTES) {
      toast.add({
        type: "error",
        title: "Ảnh quá lớn",
        description: "Vui lòng chọn ảnh dưới 5MB.",
      });
      return;
    }

    if (imagePreviewUrl) {
      URL.revokeObjectURL(imagePreviewUrl);
    }

    setSelectedImage(file);
    setImagePreviewUrl(URL.createObjectURL(file));
  };

  const handleRemoveSelectedImage = () => {
    if (imagePreviewUrl) {
      URL.revokeObjectURL(imagePreviewUrl);
    }
    setSelectedImage(null);
    setImagePreviewUrl(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const handleInsertEmoji = (emoji: string) => {
    const textarea = textareaRef.current;
    if (!textarea) {
      setDraft((prev) => (prev + emoji).slice(0, MAX_LEN));
      return;
    }
    const start = textarea.selectionStart ?? draft.length;
    const end = textarea.selectionEnd ?? draft.length;
    const next = draft.slice(0, start) + emoji + draft.slice(end);
    if (next.length > MAX_LEN) return;
    setDraft(next);
    requestAnimationFrame(() => {
      textarea.focus();
      const newPos = start + emoji.length;
      textarea.setSelectionRange(newPos, newPos);
    });
  };

  const send = async () => {
    const content = draft.trim();
    if ((!content && !selectedImage) || sending || restriction?.isRestricted) return;
    setSending(true);
    try {
      let saved: CommunityMessageDto;
      if (selectedImage) {
        saved = await communityApi.sendImage(selectedImage, content || undefined, replyingTo?.id);
        handleRemoveSelectedImage();
      } else {
        saved = await communityApi.send(content, replyingTo?.id);
      }
      mergeMessages([{ ...saved, isMine: true }], { isMine: true });
      setDraft("");
      setReplyingTo(null);
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không gửi được",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setSending(false);
    }
  };

  const openReportDialog = (msg: CommunityMessageDto) => {
    setReportTarget(msg);
    setReportReason("");
    setReportDetail("");
  };

  const handleSendReport = async () => {
    if (!reportTarget) return;
    const reason = reportReason.trim();
    if (reason.length < 3 || reason.length > 64) {
      toast.add({
        type: "error",
        title: "Lý do không hợp lệ",
        description: "Lý do báo cáo phải từ 3 đến 64 ký tự.",
      });
      return;
    }

    setSubmittingReport(true);
    try {
      await communityApi.report(reportTarget.id, reason, reportDetail.trim() || undefined);
      setReported((prev) => new Set(prev).add(reportTarget.id));
      toast.add({ type: "success", title: "Đã gửi báo cáo vi phạm" });
      setReportTarget(null);
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không gửi được báo cáo",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setSubmittingReport(false);
    }
  };

  const handleModeratorHide = async (messageId: string) => {
    setHidingId(messageId);
    setConfirmingHideId(null);
    try {
      await adminForumApi.hideMessage(messageId);
      setMessages((prev) => prev.filter((m) => m.id !== messageId));
      toast.add({ type: "success", title: "Đã ẩn tin nhắn" });
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không ẩn được tin",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setHidingId(null);
    }
  };

  const canSend = !restriction?.isRestricted && !!user;
  const hasContent = !!draft.trim() || !!selectedImage;

  // Chỉ phục vụ giao diện: bảng thành viên (cột phải trên lg, Sheet trên màn nhỏ)
  const [showMembers, setShowMembers] = useState(true);
  const headerHidden = useHeaderHidden();
  const [showRules, setShowRules] = useState(true);

  // Màu vai trò cho trích dẫn trả lời (DTO trả lời không kèm vai trò)
  const roleByUserId = useMemo(() => {
    const map = new Map<string, string>();
    for (const m of messages) {
      if (m.userId && m.authorRole) map.set(m.userId, m.authorRole);
    }
    return map;
  }, [messages]);

  const restrictionNotice = useMemo(() => {
    if (!restriction?.isRestricted) return null;
    let timeText = "";
    if (restriction.restrictedUntil) {
      const d = new Date(restriction.restrictedUntil);
      if (!Number.isNaN(d.getTime())) {
        timeText = d.toLocaleString("vi-VN", {
          hour: "2-digit",
          minute: "2-digit",
          day: "2-digit",
          month: "2-digit",
          year: "numeric",
        });
      }
    }
    return (
      <div className="flex min-h-11 items-center gap-2.5 rounded-xl bg-surface px-4 py-2.5 text-sm text-ink-2">
        <LockIcon className="size-4 shrink-0 text-ink-3" aria-hidden />
        <p className="min-w-0">
          <span className="font-medium text-ink">Bạn bị hạn chế chat{timeText ? ` đến ${timeText}` : ""}</span>
          {restriction.reason ? <span>. Lý do: {restriction.reason}</span> : null}
        </p>
      </div>
    );
  }, [restriction]);

  const replyingRole = replyingTo?.userId ? (roleByUserId.get(replyingTo.userId) ?? "") : "";
  const showWelcome = !loading && (!hasMore || messages.length === 0);

  return (
    <ProtectedRoute>
      {/* ponytail: -1px = SiteHeader border-b; --header-h chỉ tính 96px nội dung. Nền: sáng hơn thanh trên ở chế độ sáng, tối hơn ở chế độ tối */}
      <section
        className={cn(
          "flex min-h-0 w-full bg-[color-mix(in_oklab,var(--brand),white_14%)] dark:bg-brand-deep",
          headerHidden ? "h-dvh" : "h-[calc(100dvh-var(--header-h)-1px)]",
        )}
      >
        {/* Nền màu nhấn tràn mép, liền với thanh điều hướng; nội dung giới hạn cùng bề rộng (max-w-[1560px]).
            Khi ẩn thanh trên cùng thì chat chiếm trọn màn hình, bỏ giới hạn bề rộng */}
        <div className={cn("mx-auto flex min-h-0 w-full flex-1 gap-2 p-2 sm:px-4", !headerHidden && "max-w-[1560px]")}>
          {/* Vỏ nền màu nhấn; khung chat, ô nhập và danh sách thành viên là các thẻ trắng bên trong */}
          <div className="flex min-h-0 w-full flex-1 gap-2">
            <div className="flex min-w-0 flex-1 flex-col gap-2">
              {/* Thanh kênh: nút ẩn thanh trên cùng | tiêu đề giữa | nội quy, thành viên */}
              <header className="grid h-9 shrink-0 grid-cols-[1fr_auto_1fr] items-center gap-2">
                <div className="flex items-center">
                  <Tip label={headerHidden ? "Hiện thanh trên cùng" : "Ẩn thanh trên cùng"}>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className={TOOLBAR_BTN}
                      aria-label={headerHidden ? "Hiện thanh trên cùng" : "Ẩn thanh trên cùng"}
                      aria-pressed={headerHidden}
                      onClick={() => setHeaderHidden(!headerHidden)}
                    >
                      <ChevronUpIcon className={cn("size-4 transition-transform", headerHidden && "rotate-180")} />
                    </Button>
                  </Tip>
                </div>
                <h1 className="truncate font-heading text-lg font-semibold text-brand-foreground">Thảo luận chung</h1>
                <div className="flex items-center justify-end gap-1">
                  <Tip label="Nội quy">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className={cn(TOOLBAR_BTN, "hidden lg:inline-flex")}
                      aria-label="Nội quy"
                      aria-pressed={showRules}
                      onClick={() => setShowRules((v) => !v)}
                    >
                      <ScrollTextIcon className="size-4" />
                    </Button>
                  </Tip>
                  <Sheet>
                    <Tip label="Nội quy">
                      <SheetTrigger
                        render={
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className={cn(TOOLBAR_BTN, "lg:hidden")}
                            aria-label="Nội quy"
                          >
                            <ScrollTextIcon className="size-4" />
                          </Button>
                        }
                      />
                    </Tip>
                    <SheetContent side="right" className="w-[300px] bg-surface p-0">
                      <SheetHeader className="border-b border-line">
                        <SheetTitle className="font-heading">Nội quy diễn đàn</SheetTitle>
                      </SheetHeader>
                      <RulesList />
                    </SheetContent>
                  </Sheet>
                  <Tip label="Thành viên">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className={cn(TOOLBAR_BTN, "hidden lg:inline-flex")}
                      aria-label="Thành viên"
                      aria-pressed={showMembers}
                      onClick={() => setShowMembers((v) => !v)}
                    >
                      <UsersIcon className="size-4" />
                    </Button>
                  </Tip>
                  <Sheet>
                    <Tip label="Thành viên">
                      <SheetTrigger
                        render={
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className={cn(TOOLBAR_BTN, "lg:hidden")}
                            aria-label="Thành viên"
                          >
                            <UsersIcon className="size-4" />
                          </Button>
                        }
                      />
                    </Tip>
                    <SheetContent side="right" className="w-[280px] bg-brand-deep p-0 text-white dark:bg-black">
                      <SheetHeader className="sr-only">
                        <SheetTitle>Đang trong phòng</SheetTitle>
                      </SheetHeader>
                      <div className="flex h-full min-h-0 flex-col pt-10">
                        <MemberPanel members={members} currentUserId={user?.id} />
                      </div>
                    </SheetContent>
                  </Sheet>
                </div>
              </header>

              {/* Hàng giữa: khung chat + nội quy; ô nhập nằm dưới cả hai */}
              <div className="flex min-h-0 flex-1 gap-2">
                {/* Thẻ khung chat */}
                <div className="relative flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden rounded-xl bg-surface">
                  {/* Dòng tin nhắn: phần tử cuộn duy nhất (listRef + onScroll) */}
                  <div
                    ref={listRef}
                    onScroll={handleScroll}
                    className="flex min-h-0 flex-1 flex-col overflow-y-auto py-4"
                  >
                    {/* ponytail: spacer anchors short histories to the composer, like Discord */}
                    <div className="mt-auto" aria-hidden />
                    {loading ? (
                      <div className="space-y-5 px-4 sm:px-6" aria-busy="true">
                        <span className="sr-only">Đang tải tin nhắn...</span>
                        {Array.from({ length: 6 }).map((_, i) => (
                          <div key={i} className="flex gap-4">
                            <Skeleton className="size-10 shrink-0 rounded-full" />
                            <div className="flex-1 space-y-2 pt-1">
                              <Skeleton className="h-3.5 w-32" />
                              <Skeleton className={cn("h-3.5", i % 2 ? "w-3/5" : "w-4/5")} />
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <>
                        {loadingOlder && (
                          <div className="flex items-center justify-center gap-2 py-2 text-xs text-ink-3">
                            <Spinner size="sm" /> Đang tải tin cũ hơn...
                          </div>
                        )}

                        {showWelcome && (
                          <div className="px-4 pb-4 pt-6 sm:px-6">
                            <h2 className="font-sans text-2xl font-semibold text-ink">
                              Chào mừng đến #Thảo luận chung
                            </h2>
                            <p className="mt-1 text-sm text-ink-2">
                              {messages.length === 0
                                ? "Chưa có tin nào. Hãy bắt đầu câu hỏi."
                                : "Đây là phần đầu của kênh. Trò chuyện cùng học sinh và phụ huynh khác."}
                            </p>
                          </div>
                        )}

                        {messages.map((m, index) => {
                          const prev = messages[index - 1];
                          const showDaySeparator = !prev || getDayKey(m.createdAt) !== getDayKey(prev.createdAt);

                          const isSameAuthor =
                            prev &&
                            prev.userId === m.userId &&
                            getDayKey(m.createdAt) === getDayKey(prev.createdAt) &&
                            Math.abs(new Date(m.createdAt).getTime() - new Date(prev.createdAt).getTime()) <
                              5 * 60 * 1000;
                          // Tin trả lời luôn có đầu dòng để gắn đường nối trả lời
                          const grouped = !!isSameAuthor && !m.replyTo;

                          const meta = roleMeta(m.authorRole);
                          const replyRole = m.replyTo?.userId ? (roleByUserId.get(m.replyTo.userId) ?? "") : "";
                          const replyMeta = roleMeta(replyRole);
                          const fullTime = formatFullDateTime(m.createdAt);

                          return (
                            <React.Fragment key={m.id}>
                              {showDaySeparator && (
                                <div role="separator" className="relative mx-4 mb-2 mt-6 border-t border-line sm:mx-6">
                                  <span className="absolute left-1/2 top-0 -translate-x-1/2 -translate-y-1/2 bg-surface px-3 text-xs font-semibold text-ink-3">
                                    {formatDaySeparator(m.createdAt)}
                                  </span>
                                </div>
                              )}
                              <div
                                className={cn(
                                  "group/msg relative px-4 hover:bg-ground focus-within:bg-ground sm:px-6",
                                  grouped ? "py-0.5" : "mt-4 pb-0.5 pt-1.5",
                                )}
                              >
                                {/* Thanh công cụ hover: Trả lời, Báo cáo, Ẩn tin */}
                                <div className="pointer-events-none absolute -top-4 right-4 z-10 flex items-center gap-0.5 rounded-md border border-line bg-surface p-0.5 opacity-0 shadow-sm group-focus-within/msg:pointer-events-auto group-focus-within/msg:opacity-100 group-hover/msg:pointer-events-auto group-hover/msg:opacity-100">
                                  <Tip label="Trả lời">
                                    <Button
                                      type="button"
                                      variant="ghost"
                                      size="icon"
                                      className="size-8 text-ink-2 hover:text-ink"
                                      aria-label="Trả lời"
                                      onClick={() => handleStartReply(m)}
                                    >
                                      <CornerUpLeftIcon className="size-4" />
                                    </Button>
                                  </Tip>
                                  {!m.isMine && (
                                    <Tip label={reported.has(m.id) ? "Đã gửi báo cáo" : "Báo cáo vi phạm"}>
                                      <Button
                                        type="button"
                                        variant="ghost"
                                        size="icon"
                                        className="size-8 text-ink-2 hover:text-risk"
                                        aria-label="Báo cáo"
                                        disabled={reported.has(m.id)}
                                        onClick={() => openReportDialog(m)}
                                      >
                                        <FlagIcon className="size-4" />
                                      </Button>
                                    </Tip>
                                  )}
                                  {isStaff && (
                                    <Tip label="Ẩn tin nhắn này">
                                      <Button
                                        type="button"
                                        variant="ghost"
                                        size="icon"
                                        className="size-8 text-ink-2 hover:text-risk"
                                        aria-label="Ẩn tin"
                                        disabled={hidingId === m.id}
                                        onClick={() => setConfirmingHideId(m.id)}
                                      >
                                        <EyeOffIcon className="size-4" />
                                      </Button>
                                    </Tip>
                                  )}
                                </div>

                                {/* Đường nối trả lời kiểu Discord */}
                                {!grouped && m.replyTo && (
                                  <div className="mb-0.5 flex h-5 items-center gap-1.5 text-[13px]">
                                    <span
                                      className="ml-5 h-1/2 w-6 shrink-0 self-end rounded-tl-lg border-l-2 border-t-2 border-line"
                                      aria-hidden
                                    />
                                    <span className={cn("shrink-0 font-semibold", replyMeta.text)}>
                                      {m.replyTo.authorName}
                                    </span>
                                    <span className="min-w-0 truncate text-ink-2">{m.replyTo.contentSnippet}</span>
                                  </div>
                                )}

                                <div className="flex gap-4">
                                  {grouped ? (
                                    <div className="w-10 shrink-0 text-center">
                                      <time
                                        dateTime={m.createdAt}
                                        title={fullTime}
                                        className="text-xs leading-[22px] tabular-nums text-ink-3 opacity-0 group-hover/msg:opacity-100"
                                      >
                                        {formatHm(m.createdAt)}
                                      </time>
                                    </div>
                                  ) : (
                                    <Avatar className="mt-0.5 size-10">
                                      <AvatarFallback className={cn("text-sm font-semibold", meta.avatar)}>
                                        {initials(m.authorName)}
                                      </AvatarFallback>
                                    </Avatar>
                                  )}
                                  <div className="min-w-0 flex-1">
                                    {!grouped && (
                                      <div className="flex flex-wrap items-center gap-x-2">
                                        <span className={cn("text-[15px] font-semibold leading-snug", meta.text)}>
                                          {m.authorName}
                                        </span>
                                        <RoleIcon role={m.authorRole} />
                                        <time dateTime={m.createdAt} title={fullTime} className="text-xs text-ink-3">
                                          {formatHeaderTime(m.createdAt)}
                                        </time>
                                      </div>
                                    )}

                                    {/* Nội dung chữ */}
                                    {m.content ? (
                                      <p className="max-w-[75ch] whitespace-pre-wrap break-words text-[15px] leading-[1.45] text-ink">
                                        {renderMessageContent(m.content)}
                                      </p>
                                    ) : null}

                                    {/* Ảnh đính kèm */}
                                    {m.imageUrl && (
                                      <button
                                        type="button"
                                        onClick={() => setLightboxUrl(m.imageUrl || null)}
                                        aria-label="Xem ảnh lớn"
                                        className="mt-1.5 block max-w-[min(400px,100%)] cursor-zoom-in rounded-lg"
                                      >
                                        {/* eslint-disable-next-line @next/next/no-img-element */}
                                        <img
                                          src={m.imageUrl}
                                          alt="Ảnh đính kèm"
                                          loading="lazy"
                                          className="max-h-[300px] max-w-full rounded-lg border border-line object-cover hover:brightness-95"
                                        />
                                      </button>
                                    )}

                                    {/* Moderator inline confirm hide */}
                                    {isStaff && confirmingHideId === m.id && (
                                      <div className="mt-1 flex min-h-7 items-center gap-3 text-xs">
                                        <span className="font-medium text-ink-2">Ẩn tin này?</span>
                                        <button
                                          type="button"
                                          disabled={hidingId === m.id}
                                          onClick={() => void handleModeratorHide(m.id)}
                                          className="min-h-7 cursor-pointer font-semibold text-risk hover:underline"
                                        >
                                          Ẩn
                                        </button>
                                        <button
                                          type="button"
                                          onClick={() => setConfirmingHideId(null)}
                                          className="min-h-7 cursor-pointer text-ink-2 hover:underline"
                                        >
                                          Hủy
                                        </button>
                                      </div>
                                    )}
                                  </div>
                                </div>
                              </div>
                            </React.Fragment>
                          );
                        })}
                      </>
                    )}
                  </div>

                  {/* Thanh tin mới: dính mép trên vùng soạn thảo */}
                  {newMessagesCount > 0 && (
                    <button
                      type="button"
                      onClick={() => {
                        setNewMessagesCount(0);
                        scrollToBottom(true);
                      }}
                      className="flex h-8 w-full shrink-0 cursor-pointer items-center justify-between bg-brand px-4 text-sm font-medium text-brand-foreground sm:px-6"
                    >
                      <span>{newMessagesCount} tin mới</span>
                      <span className="flex items-center gap-1">
                        Xem ngay <ArrowDownIcon className="size-4" aria-hidden />
                      </span>
                    </button>
                  )}
                </div>
                {/* Thẻ nội quy (lg trở lên): mở sẵn khi vào trang, ẩn được bằng nút ở thanh kênh */}
                <aside
                  aria-label="Nội quy diễn đàn"
                  inert={!showRules}
                  className={cn(SIDE_PANEL, showRules ? SIDE_OPEN : SIDE_CLOSED)}
                >
                  <div className="flex h-full w-72 flex-col overflow-hidden rounded-xl bg-surface">
                    <h2 className="flex h-12 shrink-0 items-center gap-2 border-b border-line px-4 font-heading text-base font-semibold text-ink">
                      <ScrollTextIcon className="size-4 text-brand" aria-hidden />
                      Nội quy diễn đàn
                    </h2>
                    <RulesList />
                  </div>
                </aside>
              </div>

              {/* Vùng soạn thảo tin nhắn: thẻ trắng riêng */}
              <div className="shrink-0">
                {restrictionNotice ? (
                  restrictionNotice
                ) : (
                  <div>
                    {/* Khung soạn kiểu khung chat AI: chữ ở trên, thanh công cụ ở dưới */}
                    <div className="overflow-hidden rounded-xl bg-surface">
                      {/* Thanh đang trả lời */}
                      {replyingTo && (
                        <div className="flex items-center gap-2 border-b border-line py-1.5 pl-4 pr-2 text-[13px] text-ink-2">
                          <CornerUpLeftIcon className="size-4 shrink-0 text-ink-3" aria-hidden />
                          <span className="shrink-0">Đang trả lời</span>
                          <span className={cn("truncate font-semibold", roleMeta(replyingRole).text)}>
                            {replyingTo.authorName}
                          </span>
                          <Tip label="Hủy trả lời">
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="ml-auto size-7 shrink-0 text-ink-2 hover:text-ink"
                              aria-label="Hủy trả lời"
                              onClick={handleCancelReply}
                            >
                              <XIcon className="size-4" />
                            </Button>
                          </Tip>
                        </div>
                      )}

                      {/* Ảnh đính kèm trước khi gửi */}
                      {imagePreviewUrl && (
                        <div className="px-4 pt-3">
                          <div className="relative size-24">
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                              src={imagePreviewUrl}
                              alt="Ảnh xem trước"
                              title={selectedImage?.name}
                              className="size-24 rounded-lg object-cover"
                            />
                            <Tip label="Bỏ ảnh">
                              <button
                                type="button"
                                onClick={handleRemoveSelectedImage}
                                aria-label="Bỏ ảnh"
                                className="absolute right-1 top-1 flex size-6 cursor-pointer items-center justify-center rounded-full bg-ink/70 text-white hover:bg-ink/85"
                              >
                                <XIcon className="size-4" />
                              </button>
                            </Tip>
                          </div>
                        </div>
                      )}

                      <Textarea
                        ref={textareaRef}
                        value={draft}
                        onChange={(e) => setDraft(e.target.value.slice(0, MAX_LEN))}
                        placeholder={selectedImage ? "Thêm chú thích…" : "Nhắn trong Thảo luận chung…"}
                        aria-label="Nội dung tin nhắn"
                        rows={2}
                        disabled={!canSend || sending}
                        className="block max-h-[40dvh] min-h-14 w-full resize-none rounded-none border-0 bg-transparent px-4 pb-3 pt-3.5 text-[15px] leading-[1.45] shadow-none outline-none focus-visible:border-transparent focus-visible:ring-0 focus-visible:outline-none disabled:bg-transparent md:text-[15px] dark:bg-transparent dark:disabled:bg-transparent"
                        onKeyDown={(e) => {
                          if (e.key === "Enter" && !e.shiftKey) {
                            e.preventDefault();
                            void send();
                          }
                        }}
                      />

                      <div className="flex items-center gap-1 border-t border-line bg-ground px-2 py-1.5">
                        <input
                          ref={fileInputRef}
                          type="file"
                          accept="image/jpeg,image/png,image/webp"
                          className="hidden"
                          onChange={handleFileSelect}
                        />
                        <Tip label="Đính kèm ảnh (JPG, PNG, WebP · tối đa 5MB)">
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            className="size-9 shrink-0 rounded-xl text-ink-2 hover:text-ink"
                            aria-label="Đính kèm ảnh"
                            disabled={!canSend || sending}
                            onClick={() => fileInputRef.current?.click()}
                          >
                            <PlusIcon className="size-5" />
                          </Button>
                        </Tip>

                        {/* Nút emoji */}
                        <Popover open={showEmojiPicker} onOpenChange={setShowEmojiPicker}>
                          <Tip label="Biểu tượng cảm xúc">
                            <PopoverTrigger
                              render={
                                <Button
                                  type="button"
                                  variant="ghost"
                                  size="icon"
                                  className="size-9 shrink-0 rounded-xl text-ink-2 hover:text-ink"
                                  aria-label="Chọn biểu tượng cảm xúc"
                                  disabled={!canSend || sending}
                                >
                                  <SmileIcon className="size-5" />
                                </Button>
                              }
                            />
                          </Tip>
                          <PopoverContent side="top" align="start" className="w-auto p-0 border-none shadow-xl">
                            <React.Suspense
                              fallback={
                                <div className="flex items-center justify-center gap-2 p-8 text-xs text-ink-3">
                                  <Spinner size="sm" /> Đang tải biểu cảm...
                                </div>
                              }
                            >
                              <EmojiPicker
                                onEmojiClick={(emojiData) => handleInsertEmoji(emojiData.emoji)}
                                autoFocusSearch={false}
                                previewConfig={{ showPreview: false }}
                                skinTonesDisabled
                                lazyLoadEmojis
                                width={320}
                                height={380}
                              />
                            </React.Suspense>
                          </PopoverContent>
                        </Popover>

                        {draft.length >= 1800 && (
                          <span
                            className={cn(
                              "ml-auto text-xs tabular-nums",
                              draft.length >= 1900 ? "text-warn" : "text-ink-3",
                            )}
                          >
                            {draft.length}/{MAX_LEN}
                          </span>
                        )}

                        <Tip label="Gửi">
                          <Button
                            type="button"
                            size="icon"
                            disabled={!canSend || sending || !hasContent}
                            onClick={() => void send()}
                            aria-label="Gửi"
                            className={cn(
                              "size-9 shrink-0 rounded-xl",
                              draft.length < 1800 && "ml-auto",
                              hasContent
                                ? "bg-brand text-brand-foreground hover:bg-brand/90"
                                : "bg-line text-ink-3 disabled:opacity-100",
                            )}
                          >
                            {sending ? <Spinner size="sm" /> : <ArrowUpIcon className="size-5" />}
                          </Button>
                        </Tip>
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Bảng thành viên (lg trở lên) */}
            <aside
              aria-label="Thành viên"
              inert={!showMembers}
              className={cn(SIDE_PANEL, showMembers ? SIDE_OPEN : SIDE_CLOSED)}
            >
              <div className="flex h-full w-72 flex-col overflow-hidden rounded-xl bg-brand-deep dark:bg-black/40">
                <MemberPanel members={members} currentUserId={user?.id} />
              </div>
            </aside>
          </div>
        </div>

        {/* Modal Popup Báo cáo tin nhắn */}
        <Dialog
          open={!!reportTarget}
          onOpenChange={(open) => !open && !submittingReport && setReportTarget(null)}
          disablePointerDismissal={submittingReport}
        >
          <DialogContent variant="form" size="sm">
            <DialogHeader>
              <DialogIcon tone="destructive">
                <FlagIcon />
              </DialogIcon>
              <DialogTitle>Báo cáo tin nhắn vi phạm</DialogTitle>
              <DialogDescription>Báo cáo sẽ được gửi tới đội ngũ kiểm duyệt viên để xem xét xử lý.</DialogDescription>
            </DialogHeader>

            <DialogBody className="space-y-3">
              {reportTarget && (
                <div className="space-y-1 rounded-md border border-line bg-ground p-2.5 text-xs text-ink-2">
                  <p className="font-semibold text-ink">{reportTarget.authorName}:</p>
                  <p className="line-clamp-2">
                    {reportTarget.content || (reportTarget.imageUrl ? "[Đính kèm hình ảnh]" : "")}
                  </p>
                </div>
              )}

              <div className="space-y-1.5">
                <label htmlFor="report-reason" className="text-xs font-medium text-ink">
                  Lý do báo cáo <span className="text-risk">*</span>
                </label>
                <Input
                  id="report-reason"
                  type="text"
                  value={reportReason}
                  onChange={(e) => setReportReason(e.target.value.slice(0, 64))}
                  placeholder="Ví dụ: Spam quảng cáo, từ ngữ thù địch, xúc phạm..."
                  className="h-9"
                />
                <p className="text-xs text-ink-3">Từ 3 đến 64 ký tự.</p>
              </div>

              <div className="space-y-1.5">
                <label htmlFor="report-detail" className="text-xs font-medium text-ink">
                  Chi tiết thêm (tùy chọn)
                </label>
                <Textarea
                  id="report-detail"
                  value={reportDetail}
                  onChange={(e) => setReportDetail(e.target.value.slice(0, 1000))}
                  placeholder="Cung cấp thêm ngữ cảnh vi phạm nếu cần..."
                  rows={2}
                  className="min-h-0 resize-none"
                />
              </div>
            </DialogBody>

            <DialogFooter layout="pair">
              <DialogCancel disabled={submittingReport}>Hủy</DialogCancel>
              <DialogAction
                type="button"
                tone="destructive"
                onClick={() => void handleSendReport()}
                loading={submittingReport}
                disabled={submittingReport || reportReason.trim().length < 3}
              >
                Gửi báo cáo
              </DialogAction>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Modal Lightbox xem ảnh lớn */}
        <Dialog open={!!lightboxUrl} onOpenChange={(open) => !open && setLightboxUrl(null)}>
          <DialogContent
            variant="action"
            size="full"
            className="h-[94dvh] max-h-[94dvh] bg-[#0f2233]/95 dark:bg-black/95 text-white ring-white/10"
          >
            <DialogHeader className="sr-only">
              <DialogTitle>Xem ảnh đính kèm</DialogTitle>
              <DialogDescription>Ảnh xem kích thước lớn</DialogDescription>
            </DialogHeader>
            <DialogBody className="flex items-center justify-center overflow-hidden p-0">
              {lightboxUrl && (
                <img
                  src={lightboxUrl}
                  alt="Ảnh phóng to"
                  className="max-h-full max-w-full w-auto h-auto object-contain rounded-lg select-none drop-shadow-2xl"
                />
              )}
            </DialogBody>
            <DialogFooter layout="single">
              <DialogCancel tone="tint" className="mx-auto max-w-[200px] bg-white/15 text-white hover:bg-white/25">
                Đóng
              </DialogCancel>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </section>
    </ProtectedRoute>
  );
}
