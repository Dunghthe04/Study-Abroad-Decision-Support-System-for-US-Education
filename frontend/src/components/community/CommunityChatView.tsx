"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ArrowDownIcon, EyeOffIcon, MessagesSquareIcon, SendIcon, UsersIcon } from "lucide-react";
import { ProtectedRoute } from "@/components/auth/ProtectedRoute";
import { ReportMessageControl } from "@/components/lead/LeadMessageParts";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
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

const MAX_LEN = 2000;

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
      <div className="p-3">
        <p className="text-xs text-muted-foreground mb-2">Chỉ có bạn trong phòng.</p>
        <ul className="space-y-1">
          <li className="flex items-center gap-2 rounded-lg px-2 py-1.5 bg-muted/40">
            <span className="relative">
              <Avatar size="sm">
                <AvatarFallback className="bg-brand-wash text-[10px] font-semibold text-brand-ink">
                  {initials(members[0].fullName)}
                </AvatarFallback>
              </Avatar>
              <span className="absolute right-0 bottom-0 size-2 rounded-full bg-ok ring-2 ring-background" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">{members[0].fullName} (Bạn)</p>
              {members[0].role ? <p className="truncate text-[11px] text-muted-foreground">{roleLabel(members[0].role)}</p> : null}
            </div>
          </li>
        </ul>
      </div>
    );
  }

  return (
    <ul className="space-y-1 p-2">
      {members.map((m) => {
        const isMe = m.userId === currentUserId;
        const lastSeen = m.lastSeenAt ? formatClockTime(m.lastSeenAt) : "Đang online";
        return (
          <li key={m.userId} className={cn("flex items-center gap-2 rounded-lg px-2 py-1.5", isMe && "bg-muted/40")}>
            <span className="relative">
              <Avatar size="sm">
                <AvatarFallback className="bg-brand-wash text-[10px] font-semibold text-brand-ink">
                  {initials(m.fullName)}
                </AvatarFallback>
              </Avatar>
              <span className="absolute right-0 bottom-0 size-2 rounded-full bg-ok ring-2 ring-background" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium">
                {m.fullName}
                {isMe ? " (Bạn)" : ""}
              </p>
              <p className="truncate text-[11px] text-muted-foreground">
                {m.role ? `${roleLabel(m.role)} · ` : ""}
                {lastSeen}
              </p>
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

  const listRef = useRef<HTMLDivElement | null>(null);
  const isNearBottomRef = useRef(true);
  const initialLoadedRef = useRef(false);
  // Bản sao danh sách hiện tại để biết tin nào thật sự mới khi đếm "Có N tin mới"
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

  // Lần tải đầu: lúc tải xong danh sách vẫn đang là vòng xoay, nên chỉ cuộn xuống tin mới nhất
  // sau khi loading = false và tin nhắn đã thật sự hiện ra
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
          el.scrollTop = el.scrollHeight - prevScrollHeight + prevScrollTop;
        }
      });
    } catch (err) {
      toast.add({
        type: "error",
        title: "Không tải được tin cũ hơn",
        description: err instanceof ApiError ? err.message : "Thử lại sau.",
      });
    } finally {
      setLoadingOlder(false);
    }
  }, [messages, loadingOlder, hasMore, mergeMessages]);

  const handleScroll = useCallback(() => {
    const el = listRef.current;
    if (!el) return;
    const distanceToBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
    const isNear = distanceToBottom <= 100;
    isNearBottomRef.current = isNear;
    if (isNear) {
      setNewMessagesCount(0);
    }
    // Infinite scroll: khi kéo lên sát đỉnh (< 60px) và còn tin cũ
    if (el.scrollTop < 60 && hasMore && !loadingOlder && !loading) {
      void loadOlder();
    }
  }, [hasMore, loadingOlder, loading, loadOlder]);

  const resync = useCallback(async () => {
    try {
      const newest = messages.length > 0 ? messages[messages.length - 1] : null;
      if (newest) {
        let lastTime = newest.createdAt;
        let lastId = newest.id;
        let fetchedTotal = 0;
        const newMessages: CommunityMessageDto[] = [];
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
          // Lỡ quá nhiều tin: thay hẳn danh sách bằng 50 tin mới nhất. Gộp vào danh sách cũ sẽ để lại
          // một khoảng hổng ở giữa mà loadOlder (chỉ tải từ tin cũ nhất trở về trước) không bao giờ lấp được.
          const latest = await communityApi.listMessages({ take: 50 });
          setMessages(
            [...latest].reverse().map((m) => ({ ...m, isMine: m.userId === user?.id || m.isMine })),
          );
          setHasMore(latest.length >= 50);
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
  }, [messages, mergeMessages, loadInitial, user?.id, scrollToBottom]);

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
          type: "warning",
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

  const send = async () => {
    const content = draft.trim();
    if (!content || sending || restriction?.isRestricted) return;
    setSending(true);
    try {
      const saved = await communityApi.send(content);
      mergeMessages([{ ...saved, isMine: true }], { isMine: true });
      setDraft("");
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
      <section className="space-y-4 pb-4">
        <div>
          <h1 className="font-heading text-h2">Hỏi đáp cộng đồng</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Phòng chat chung cho thành viên đã đăng nhập. Hỏi nhanh về du học, hồ sơ, trường — không thay thế trợ lý AI.
          </p>
        </div>
        <Card className="overflow-hidden py-0 h-[calc(100vh-14rem)] min-h-[500px] max-h-[850px] flex flex-col">
          <CardHeader className="flex-row items-center justify-between gap-3 border-b py-3 shrink-0">
            <div className="flex items-center gap-2">
              <MessagesSquareIcon className="size-4 text-brand" aria-hidden />
              <div>
                <CardTitle className="text-base">Phòng chung</CardTitle>
                <CardDescription>Tin nhắn realtime, lưu trên diễn đàn</CardDescription>
              </div>
            </div>
            <Sheet>
              <SheetTrigger render={<Button variant="outline" size="sm" className="lg:hidden gap-1.5" />}>
                <UsersIcon className="size-3.5" />
                {memberCount}
              </SheetTrigger>
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
                                  "group flex gap-2.5 rounded-lg px-2 py-1.5 transition-colors",
                                  m.isMine && "bg-brand-wash/50",
                                  isSameAuthor && "mt-0.5 pt-0.5",
                                )}
                              >
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
                                  <p className="mt-0.5 whitespace-pre-wrap break-words text-sm leading-relaxed">
                                    {renderMessageContent(m.content)}
                                  </p>
                                  <div className="mt-1 flex items-center gap-3">
                                    {!m.isMine && (
                                      <ReportMessageControl
                                        isReported={reported.has(m.id)}
                                        onReport={async (reason) => {
                                          try {
                                            await communityApi.report(m.id, reason.trim() || "Vi phạm");
                                            setReported((prev) => new Set(prev).add(m.id));
                                            toast.add({ type: "success", title: "Đã gửi báo cáo" });
                                          } catch (err) {
                                            toast.add({
                                              type: "error",
                                              title: "Không gửi được báo cáo",
                                              description: err instanceof ApiError ? err.message : "Thử lại sau.",
                                            });
                                            throw err;
                                          }
                                        }}
                                      />
                                    )}
                                    {isStaff && (
                                      <>
                                        {confirmingHideId === m.id ? (
                                          <div className="flex items-center gap-1.5 bg-destructive/10 px-2 py-0.5 rounded text-[11px]">
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
                                        ) : (
                                          <button
                                            type="button"
                                            disabled={hidingId === m.id}
                                            onClick={() => setConfirmingHideId(m.id)}
                                            className="inline-flex cursor-pointer items-center gap-1 text-[11px] text-muted-foreground hover:text-destructive focus-visible:outline-none"
                                            title="Ẩn tin nhắn này"
                                          >
                                            <EyeOffIcon className="size-3" aria-hidden />
                                            Ẩn tin
                                          </button>
                                        )}
                                      </>
                                    )}
                                  </div>
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
                  className="absolute bottom-16 left-1/2 -translate-x-1/2 shadow-lg bg-brand text-brand-foreground hover:bg-brand/90 px-3 py-1.5 rounded-full text-xs font-medium flex items-center gap-1.5 transition-all animate-in fade-in slide-in-from-bottom-2 z-10 cursor-pointer"
                >
                  <ArrowDownIcon className="size-3.5" />
                  <span>Có {newMessagesCount} tin mới</span>
                </button>
              )}

              <div className="border-t p-3 shrink-0">
                {restrictionNotice ? (
                  restrictionNotice
                ) : (
                  <div className="flex items-end gap-2">
                    <Textarea
                      value={draft}
                      onChange={(e) => setDraft(e.target.value.slice(0, MAX_LEN))}
                      placeholder="Viết tin nhắn công khai..."
                      rows={2}
                      disabled={!canSend || sending}
                      className="min-h-12 resize-none"
                      onKeyDown={(e) => {
                        if (e.key === "Enter" && !e.shiftKey) {
                          e.preventDefault();
                          void send();
                        }
                      }}
                    />
                    <Button type="button" size="icon" disabled={!canSend || sending || !draft.trim()} onClick={() => void send()} aria-label="Gửi">
                      <SendIcon className="size-4" />
                    </Button>
                  </div>
                )}
              </div>
            </div>
            <aside className="hidden min-h-0 h-full border-l lg:block">{sidebar}</aside>
          </CardContent>
        </Card>
      </section>
    </ProtectedRoute>
  );
}
