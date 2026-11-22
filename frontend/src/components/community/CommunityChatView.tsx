"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowDownIcon,
  EyeOffIcon,
  FlagIcon,
  ImageIcon,
  MessagesSquareIcon,
  QuoteIcon,
  SendIcon,
  SmileIcon,
  UsersIcon,
  XIcon,
} from "lucide-react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/toast";
import { useAuth } from "@/contexts/AuthContext";
import { useCommunityChat } from "@/hooks/use-community-chat";
import { ApiError } from "@/lib/api";
import { adminForumApi, communityApi } from "@/lib/community-api";
import { formatUserRole } from "@/lib/user-role-labels";
import { cn } from "@/lib/utils";
import type { UserRole } from "@/types/api";
import type { CommunityMessageDto, CommunityPresenceDto, CommunityRestrictionDto } from "@/types/community";

const EmojiPicker = React.lazy(() => import("emoji-picker-react"));

const MAX_LEN = 2000;
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp"];

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

/** Giờ định dạng theo kiểu "Lúc 06:42" */
function formatClockTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const timeStr = d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
  return `Lúc ${timeStr}`;
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
  return d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
}

function roleLabel(role: string) {
  if (!role) return "";
  return formatUserRole(role as UserRole);
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

function MemberList({ members, currentUserId }: { members: CommunityPresenceDto[]; currentUserId?: string }) {
  if (members.length === 0) {
    return <p className="px-3 py-6 text-sm text-muted-foreground">Chưa có ai trong phòng.</p>;
  }
  if (members.length === 1 && members[0].userId === currentUserId) {
    return (
      <div className="px-3 py-4 text-xs text-muted-foreground space-y-2">
        <p className="font-medium text-foreground">Bạn đang ở trong phòng một mình.</p>
        <p>Khi có người khác tham gia, họ sẽ xuất hiện tại đây.</p>
      </div>
    );
  }

  return (
    <ul className="divide-y text-sm">
      {members.map((m) => {
        const isSelf = m.userId === currentUserId;
        const lastSeen = m.lastSeenAt ? new Date(m.lastSeenAt) : null;
        let lastSeenText = "Đang online";
        if (lastSeen && !Number.isNaN(lastSeen.getTime())) {
          const diffMs = Date.now() - lastSeen.getTime();
          if (diffMs < 2 * 60 * 1000) {
            lastSeenText = "Vừa xong";
          } else if (diffMs < 60 * 60 * 1000) {
            lastSeenText = `${Math.floor(diffMs / (60 * 1000))} phút trước`;
          } else {
            lastSeenText = lastSeen.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" });
          }
        }

        return (
          <li key={m.userId} className={cn("flex items-center gap-2.5 px-3 py-2", isSelf && "bg-muted/40")}>
            <div className="relative">
              <Avatar size="sm">
                <AvatarFallback className="text-[10px]">{initials(m.fullName)}</AvatarFallback>
              </Avatar>
              <span
                className="absolute bottom-0 right-0 size-2 rounded-full bg-emerald-500 ring-2 ring-background"
                title="Đang hoạt động"
                aria-hidden
              />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5">
                <p className="truncate text-xs font-medium leading-none">
                  {m.fullName}
                  {isSelf ? " (bạn)" : ""}
                </p>
                {m.role ? (
                  <Badge variant="neutral" className="h-4 px-1 text-[9px]">
                    {roleLabel(m.role)}
                  </Badge>
                ) : null}
              </div>
              <p className="mt-0.5 text-[10px] text-muted-foreground">{lastSeenText}</p>
            </div>
          </li>
        );
      })}
    </ul>
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
          const batch = await communityApi.listMessages({ after: lastTime, afterId: lastId, take: 50 });
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
            [...latestBatch].reverse().map((m) => ({ ...m, isMine: m.userId === user?.id || m.isMine })),
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

  const memberCount = members.length;
  const canSend = !restriction?.isRestricted && !!user;

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
      <p className="text-sm text-destructive">
        Bạn đang bị hạn chế chat {timeText ? `đến ${timeText}` : ""}.
        {restriction.reason ? ` Lý do: ${restriction.reason}` : ""}
      </p>
    );
  }, [restriction]);

  const sidebar = useMemo(
    () => (
      <div className="flex h-full min-h-0 flex-col">
        <p className="border-b px-3 py-2 text-sm font-medium">Đang trong phòng ({memberCount})</p>
        <div className="min-h-0 flex-1 overflow-y-auto">
          <MemberList members={members} currentUserId={user?.id} />
        </div>
      </div>
    ),
    [memberCount, members, user?.id],
  );

  return (
    <ProtectedRoute>
      <section className="container mx-auto flex max-w-6xl flex-col gap-4 py-6">
        <Card className="flex flex-col h-[calc(100vh-14rem)] min-h-[500px] max-h-[850px]">
          <CardHeader className="flex flex-row items-center justify-between gap-4 border-b pb-4">
            <div className="flex items-center gap-3">
              <div className="flex size-10 items-center justify-center rounded-lg bg-brand-wash text-brand-ink">
                <MessagesSquareIcon className="size-5" />
              </div>
              <div>
                <CardTitle className="text-lg">Kênh thảo luận chung</CardTitle>
                <CardDescription>Trò chuyện trực tiếp cùng học sinh và phụ huynh khác.</CardDescription>
              </div>
            </div>
            <Sheet>
              <SheetTrigger
                render={
                  <Button variant="outline" size="sm" className="lg:hidden cursor-pointer">
                    <UsersIcon className="size-4" />
                    <span>{memberCount}</span>
                  </Button>
                }
              />
              <SheetContent side="right" className="w-[280px] p-0">
                <SheetHeader className="sr-only">
                  <SheetTitle>Đang trong phòng</SheetTitle>
                </SheetHeader>
                {sidebar}
              </SheetContent>
            </Sheet>
          </CardHeader>
          <CardContent className="grid min-h-0 flex-1 p-0 lg:grid-cols-[1fr_260px]">
            <div className="relative flex min-h-0 h-full flex-col">
              <div ref={listRef} onScroll={handleScroll} className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
                {loading ? (
                  <div className="flex h-full items-center justify-center gap-2 text-sm text-muted-foreground">
                    <Spinner /> Đang tải tin nhắn...
                  </div>
                ) : (
                  <>
                    {loadingOlder && (
                      <div className="py-2 flex items-center justify-center gap-2 text-xs text-muted-foreground">
                        <Spinner size="sm" /> Đang tải tin cũ hơn...
                      </div>
                    )}
                    {messages.length === 0 ? (
                      <p className="py-12 text-center text-sm text-muted-foreground">Chưa có tin nào. Hãy bắt đầu câu hỏi.</p>
                    ) : (
                      <div className="space-y-3">
                        {messages.map((m, index) => {
                          const prev = messages[index - 1];
                          const showDaySeparator = !prev || getDayKey(m.createdAt) !== getDayKey(prev.createdAt);

                          const isSameAuthor =
                            prev &&
                            prev.userId === m.userId &&
                            getDayKey(m.createdAt) === getDayKey(prev.createdAt) &&
                            Math.abs(new Date(m.createdAt).getTime() - new Date(prev.createdAt).getTime()) < 5 * 60 * 1000;

                          return (
                            <React.Fragment key={m.id}>
                              {showDaySeparator && (
                                <div className="my-4 flex items-center justify-center">
                                  <span className="rounded-full bg-muted px-3 py-1 text-[11px] font-medium text-muted-foreground">
                                    {formatDaySeparator(m.createdAt)}
                                  </span>
                                </div>
                              )}
                              <div
                                className={cn(
                                  "group/msg relative flex gap-2.5 rounded-lg px-2.5 py-1.5 transition-colors hover:bg-muted/40",
                                  m.isMine && "bg-brand-wash/40 hover:bg-brand-wash/60",
                                  isSameAuthor && "mt-0.5 pt-0.5",
                                )}
                              >
                                {/* Thanh công cụ hover: Trả lời & Báo cáo & Ẩn tin */}
                                <div className="absolute right-2 -top-3 hidden group-hover/msg:flex items-center gap-0.5 rounded-md border border-border/70 bg-background px-1 py-0.5 shadow-sm z-10 animate-in fade-in zoom-in-95">
                                  <button
                                    type="button"
                                    onClick={() => handleStartReply(m)}
                                    className="p-1 text-muted-foreground hover:text-foreground hover:bg-muted rounded transition-colors cursor-pointer"
                                    title="Trả lời tin nhắn này"
                                    aria-label="Trả lời"
                                  >
                                    <QuoteIcon className="size-3.5" />
                                  </button>
                                  {!m.isMine && (
                                    <button
                                      type="button"
                                      disabled={reported.has(m.id)}
                                      onClick={() => openReportDialog(m)}
                                      className={cn(
                                        "p-1 rounded transition-colors cursor-pointer",
                                        reported.has(m.id)
                                          ? "text-muted-foreground/40 cursor-not-allowed"
                                          : "text-muted-foreground hover:text-destructive hover:bg-destructive/10",
                                      )}
                                      title={reported.has(m.id) ? "Đã gửi báo cáo" : "Báo cáo vi phạm"}
                                      aria-label="Báo cáo"
                                    >
                                      <FlagIcon className="size-3.5" />
                                    </button>
                                  )}
                                  {isStaff && (
                                    <button
                                      type="button"
                                      disabled={hidingId === m.id}
                                      onClick={() => setConfirmingHideId(m.id)}
                                      className="p-1 text-muted-foreground hover:text-destructive hover:bg-destructive/10 rounded transition-colors cursor-pointer"
                                      title="Ẩn tin nhắn này"
                                      aria-label="Ẩn tin"
                                    >
                                      <EyeOffIcon className="size-3.5" />
                                    </button>
                                  )}
                                </div>

                                {isSameAuthor ? (
                                  <div className="w-8 flex-shrink-0" />
                                ) : (
                                  <Avatar size="sm" className="mt-0.5">
                                    <AvatarFallback className="bg-brand-wash text-[10px] font-semibold text-brand-ink">
                                      {initials(m.authorName)}
                                    </AvatarFallback>
                                  </Avatar>
                                )}
                                <div className="min-w-0 flex-1">
                                  {!isSameAuthor && (
                                    <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                                      <span className="text-sm font-semibold">{m.authorName}</span>
                                      {m.authorRole ? (
                                        <Badge variant="neutral" className="h-4 px-1.5 text-[10px]">
                                          {roleLabel(m.authorRole)}
                                        </Badge>
                                      ) : null}
                                      <span className="text-[11px] text-muted-foreground">{formatClockTime(m.createdAt)}</span>
                                    </div>
                                  )}

                                  {/* Hiển thị trích dẫn trả lời */}
                                  {m.replyTo && (
                                    <div className="mt-1 flex items-start gap-1.5 rounded-r bg-muted/60 border-l-2 border-brand/60 px-2 py-1 text-xs text-muted-foreground">
                                      <QuoteIcon className="size-3 text-brand shrink-0 mt-0.5" />
                                      <div className="min-w-0">
                                        <span className="font-medium text-foreground">{m.replyTo.authorName}: </span>
                                        <span className="truncate inline-block max-w-[340px] align-bottom">
                                          {m.replyTo.contentSnippet}
                                        </span>
                                      </div>
                                    </div>
                                  )}

                                  {/* Nội dung chữ */}
                                  {m.content ? (
                                    <p className="mt-0.5 whitespace-pre-wrap break-words text-sm leading-relaxed">
                                      {renderMessageContent(m.content)}
                                    </p>
                                  ) : null}

                                  {/* Ảnh đính kèm */}
                                  {m.imageUrl && (
                                    <div className="mt-1.5">
                                      <img
                                        src={m.imageUrl}
                                        alt="Ảnh đính kèm"
                                        loading="lazy"
                                        onClick={() => setLightboxUrl(m.imageUrl || null)}
                                        className="max-h-64 max-w-sm rounded-lg border border-border/50 object-cover shadow-xs cursor-pointer hover:opacity-95 transition-opacity"
                                      />
                                    </div>
                                  )}

                                  {/* Moderator inline confirm hide */}
                                  {isStaff && confirmingHideId === m.id && (
                                    <div className="mt-1 flex items-center gap-1.5 bg-destructive/10 px-2 py-0.5 rounded text-[11px] w-fit">
                                      <span className="text-destructive font-medium">Ẩn tin này?</span>
                                      <button
                                        type="button"
                                        disabled={hidingId === m.id}
                                        onClick={() => void handleModeratorHide(m.id)}
                                        className="text-destructive font-semibold hover:underline cursor-pointer"
                                      >
                                        Có
                                      </button>
                                      <span className="text-muted-foreground">·</span>
                                      <button
                                        type="button"
                                        onClick={() => setConfirmingHideId(null)}
                                        className="text-muted-foreground hover:underline cursor-pointer"
                                      >
                                        Hủy
                                      </button>
                                    </div>
                                  )}
                                </div>
                              </div>
                            </React.Fragment>
                          );
                        })}
                      </div>
                    )}
                  </>
                )}
              </div>

              {/* Nút nổi báo có tin mới khi người dùng đang cuộn đọc tin cũ */}
              {newMessagesCount > 0 && (
                <button
                  type="button"
                  onClick={() => {
                    setNewMessagesCount(0);
                    scrollToBottom(true);
                  }}
                  className="absolute bottom-20 left-1/2 -translate-x-1/2 shadow-lg bg-brand text-brand-foreground hover:bg-brand/90 px-3 py-1.5 rounded-full text-xs font-medium flex items-center gap-1.5 transition-all animate-in fade-in slide-in-from-bottom-2 z-10 cursor-pointer"
                >
                  <ArrowDownIcon className="size-3.5" />
                  <span>Có {newMessagesCount} tin mới</span>
                </button>
              )}

              {/* Vùng soạn thảo tin nhắn */}
              <div className="border-t p-3 shrink-0 bg-background/50">
                {restrictionNotice ? (
                  restrictionNotice
                ) : (
                  <div className="flex flex-col gap-2">
                    {/* Thanh thông báo đang trả lời */}
                    {replyingTo && (
                      <div className="flex items-center justify-between rounded-md bg-muted/80 px-2.5 py-1 text-xs border border-border/50 animate-in fade-in">
                        <div className="flex items-center gap-1.5 min-w-0">
                          <QuoteIcon className="size-3 text-brand shrink-0" />
                          <span className="font-semibold text-brand truncate max-w-[120px]">{replyingTo.authorName}:</span>
                          <span className="text-muted-foreground truncate max-w-[320px]">
                            {replyingTo.content || (replyingTo.imageUrl ? "[Hình ảnh]" : "")}
                          </span>
                        </div>
                        <button
                          type="button"
                          onClick={handleCancelReply}
                          className="text-muted-foreground hover:text-foreground p-0.5 rounded cursor-pointer"
                          title="Hủy trả lời"
                        >
                          <XIcon className="size-3.5" />
                        </button>
                      </div>
                    )}

                    {/* Preview ảnh đính kèm trước khi gửi */}
                    {imagePreviewUrl && (
                      <div className="flex items-center gap-2 animate-in fade-in">
                        <div className="relative group/thumb inline-block">
                          <img
                            src={imagePreviewUrl}
                            alt="Ảnh xem trước"
                            className="h-16 w-16 object-cover rounded-md border border-border shadow-xs"
                          />
                          <button
                            type="button"
                            onClick={handleRemoveSelectedImage}
                            className="absolute -top-1.5 -right-1.5 bg-destructive text-destructive-foreground rounded-full p-0.5 shadow-md hover:bg-destructive/90 cursor-pointer"
                            title="Bỏ ảnh"
                          >
                            <XIcon className="size-3" />
                          </button>
                        </div>
                        <span className="text-xs text-muted-foreground truncate max-w-[200px]">
                          {selectedImage?.name}
                        </span>
                      </div>
                    )}

                    <div className="flex items-end gap-2">
                      {/* Nút gửi ảnh */}
                      <input
                        ref={fileInputRef}
                        type="file"
                        accept="image/jpeg,image/png,image/webp"
                        className="hidden"
                        onChange={handleFileSelect}
                      />
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="size-9 shrink-0 text-muted-foreground hover:text-foreground cursor-pointer"
                        disabled={!canSend || sending}
                        onClick={() => fileInputRef.current?.click()}
                        title="Đính kèm ảnh (tối đa 5MB)"
                      >
                        <ImageIcon className="size-4" />
                      </Button>

                      {/* Nút emoji */}
                      <Popover open={showEmojiPicker} onOpenChange={setShowEmojiPicker}>
                        <PopoverTrigger
                          render={
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon"
                              className="size-9 shrink-0 text-muted-foreground hover:text-foreground cursor-pointer"
                              disabled={!canSend || sending}
                              title="Chọn biểu cảm emoji"
                            >
                              <SmileIcon className="size-4" />
                            </Button>
                          }
                        />
                        <PopoverContent side="top" align="start" className="w-auto p-0 border-none shadow-xl">
                          <React.Suspense
                            fallback={
                              <div className="flex items-center justify-center p-8 text-xs text-muted-foreground gap-2">
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

                      <Textarea
                        ref={textareaRef}
                        value={draft}
                        onChange={(e) => setDraft(e.target.value.slice(0, MAX_LEN))}
                        placeholder={selectedImage ? "Thêm chú thích cho ảnh..." : "Viết tin nhắn công khai..."}
                        rows={2}
                        disabled={!canSend || sending}
                        className="min-h-12 resize-none flex-1"
                        onKeyDown={(e) => {
                          if (e.key === "Enter" && !e.shiftKey) {
                            e.preventDefault();
                            void send();
                          }
                        }}
                      />
                      <Button
                        type="button"
                        size="icon"
                        disabled={!canSend || sending || (!draft.trim() && !selectedImage)}
                        onClick={() => void send()}
                        aria-label="Gửi"
                        className="size-9 shrink-0 cursor-pointer"
                      >
                        {sending ? <Spinner size="sm" /> : <SendIcon className="size-4" />}
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            </div>
            <aside className="hidden min-h-0 h-full border-l lg:block">{sidebar}</aside>
          </CardContent>
        </Card>

        {/* Modal Popup Báo cáo tin nhắn */}
        <Dialog open={!!reportTarget} onOpenChange={(open) => !open && setReportTarget(null)}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>Báo cáo tin nhắn vi phạm</DialogTitle>
              <DialogDescription>
                Báo cáo sẽ được gửi tới đội ngũ kiểm duyệt viên để xem xét xử lý.
              </DialogDescription>
            </DialogHeader>

            {reportTarget && (
              <div className="rounded-md bg-muted/70 p-2.5 text-xs text-muted-foreground border border-border/40 space-y-1">
                <p className="font-semibold text-foreground">{reportTarget.authorName}:</p>
                <p className="line-clamp-2">
                  {reportTarget.content || (reportTarget.imageUrl ? "[Đính kèm hình ảnh]" : "")}
                </p>
              </div>
            )}

            <div className="space-y-3 py-1">
              <div className="space-y-1.5">
                <label htmlFor="report-reason" className="text-xs font-medium">
                  Lý do báo cáo <span className="text-destructive">*</span>
                </label>
                <input
                  id="report-reason"
                  type="text"
                  value={reportReason}
                  onChange={(e) => setReportReason(e.target.value.slice(0, 64))}
                  placeholder="Ví dụ: Spam quảng cáo, từ ngữ thù địch, xúc phạm..."
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-xs ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
                <p className="text-[10px] text-muted-foreground">Từ 3 đến 64 ký tự.</p>
              </div>

              <div className="space-y-1.5">
                <label htmlFor="report-detail" className="text-xs font-medium">
                  Chi tiết thêm (tùy chọn)
                </label>
                <textarea
                  id="report-detail"
                  value={reportDetail}
                  onChange={(e) => setReportDetail(e.target.value.slice(0, 1000))}
                  placeholder="Cung cấp thêm ngữ cảnh vi phạm nếu cần..."
                  rows={2}
                  className="w-full rounded-md border border-input bg-background px-3 py-2 text-xs ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring resize-none"
                />
              </div>
            </div>

            <DialogFooter className="gap-2 sm:gap-0">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setReportTarget(null)}
                disabled={submittingReport}
                className="cursor-pointer"
              >
                Hủy
              </Button>
              <Button
                type="button"
                variant="destructive"
                size="sm"
                onClick={() => void handleSendReport()}
                disabled={submittingReport || reportReason.trim().length < 3}
                className="cursor-pointer"
              >
                {submittingReport ? <Spinner size="sm" /> : "Gửi báo cáo"}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Modal Lightbox xem ảnh lớn */}
        <Dialog open={!!lightboxUrl} onOpenChange={(open) => !open && setLightboxUrl(null)}>
          <DialogContent className="max-w-4xl p-2 bg-background/95 backdrop-blur-md border-none flex flex-col items-center">
            <DialogHeader className="sr-only">
              <DialogTitle>Xem ảnh đính kèm</DialogTitle>
              <DialogDescription>Ảnh xem kích thước lớn</DialogDescription>
            </DialogHeader>
            {lightboxUrl && (
              <img
                src={lightboxUrl}
                alt="Ảnh phóng to"
                className="max-h-[80vh] max-w-full rounded-md object-contain shadow-2xl"
              />
            )}
          </DialogContent>
        </Dialog>
      </section>
    </ProtectedRoute>
  );
}
