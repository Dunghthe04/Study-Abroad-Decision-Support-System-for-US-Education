"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { MessagesSquareIcon, SendIcon, UsersIcon } from "lucide-react";
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
import { communityApi } from "@/lib/community-api";
import { formatUserRole } from "@/lib/user-role-labels";
import { cn } from "@/lib/utils";
import type { UserRole } from "@/types/api";
import type { CommunityMessageDto, CommunityPresenceDto } from "@/types/community";

const MAX_LEN = 2000;

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function formatTime(iso: string) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleString("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" });
}

function roleLabel(role: string) {
  if (!role) return "";
  return formatUserRole(role as UserRole);
}

function MemberList({ members }: { members: CommunityPresenceDto[] }) {
  if (members.length === 0) {
    return <p className="px-3 py-6 text-sm text-muted-foreground">Chưa có ai khác trong phòng.</p>;
  }
  return (
    <ul className="space-y-1 p-2">
      {members.map((m) => (
        <li key={m.userId} className="flex items-center gap-2 rounded-lg px-2 py-1.5">
          <span className="relative">
            <Avatar size="sm">
              <AvatarFallback className="bg-brand-wash text-[10px] font-semibold text-brand-ink">
                {initials(m.fullName)}
              </AvatarFallback>
            </Avatar>
            <span className="absolute right-0 bottom-0 size-2 rounded-full bg-ok ring-2 ring-background" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{m.fullName}</p>
            {m.role ? <p className="truncate text-[11px] text-muted-foreground">{roleLabel(m.role)}</p> : null}
          </div>
        </li>
      ))}
    </ul>
  );
}

export function CommunityChatView() {
  const { user } = useAuth();
  const [messages, setMessages] = useState<CommunityMessageDto[]>([]);
  const [members, setMembers] = useState<CommunityPresenceDto[]>([]);
  const [restriction, setRestriction] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [reported, setReported] = useState<Set<string>>(() => new Set());
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const listRef = useRef<HTMLDivElement | null>(null);

  const mergeMessages = useCallback((incoming: CommunityMessageDto[], prepend = false) => {
    setMessages((prev) => {
      const map = new Map(prev.map((m) => [m.id, m]));
      for (const item of incoming) {
        const mine = item.userId === user?.id;
        map.set(item.id, { ...item, isMine: mine || item.isMine });
      }
      const next = [...map.values()].sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id));
      if (prepend) return next;
      return next;
    });
  }, [user?.id]);

  const load = useCallback(async (before?: string) => {
    const batch = await communityApi.listMessages(before, 50);
    const chronological = [...batch].reverse();
    mergeMessages(chronological, Boolean(before));
    setHasMore(batch.length >= 50);
    return chronological;
  }, [mergeMessages]);

  const resync = useCallback(() => {
    void load().catch(() => undefined);
    void communityApi.restriction().then((r) => setRestriction(r.message)).catch(() => undefined);
  }, [load]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        await load();
        const r = await communityApi.restriction();
        if (!cancelled) setRestriction(r.message);
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
  }, [load]);

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
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [messages.length, loading]);

  const send = async () => {
    const content = draft.trim();
    if (!content || sending || restriction) return;
    setSending(true);
    try {
      const saved = await communityApi.send(content);
      mergeMessages([{ ...saved, isMine: true }]);
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

  const loadOlder = async () => {
    const oldest = messages[0];
    if (!oldest || loadingOlder || !hasMore) return;
    const el = listRef.current;
    const prevHeight = el?.scrollHeight ?? 0;
    setLoadingOlder(true);
    try {
      await load(oldest.createdAt);
      requestAnimationFrame(() => {
        if (el) el.scrollTop = el.scrollHeight - prevHeight;
      });
    } finally {
      setLoadingOlder(false);
    }
  };

  const memberCount = members.length;
  const canSend = !restriction && !!user;

  const sidebar = useMemo(
    () => (
      <div className="flex h-full min-h-0 flex-col">
        <p className="border-b px-3 py-2 text-sm font-medium">Đang trong phòng ({memberCount})</p>
        <div className="min-h-0 flex-1 overflow-y-auto">
          <MemberList members={members} />
        </div>
      </div>
    ),
    [memberCount, members],
  );

  return (
    <ProtectedRoute>
      <section className="space-y-4 pb-8">
        <div>
          <h1 className="font-heading text-h2">Hỏi đáp cộng đồng</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Phòng chat chung cho thành viên đã đăng nhập. Hỏi nhanh về du học, hồ sơ, trường — không thay thế trợ lý AI.
          </p>
        </div>
        <Card className="overflow-hidden py-0">
          <CardHeader className="flex-row items-center justify-between gap-3 border-b py-3">
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
          <CardContent className="grid p-0 lg:grid-cols-[1fr_260px]">
            <div className="flex min-h-[min(70vh,640px)] flex-col">
              <div ref={listRef} className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
                {loading ? (
                  <div className="flex h-full items-center justify-center gap-2 text-sm text-muted-foreground">
                    <Spinner /> Đang tải tin nhắn...
                  </div>
                ) : (
                  <>
                    {hasMore && (
                      <div className="mb-3 flex justify-center">
                        <Button variant="ghost" size="sm" disabled={loadingOlder} onClick={() => void loadOlder()}>
                          {loadingOlder ? "Đang tải..." : "Tin cũ hơn"}
                        </Button>
                      </div>
                    )}
                    {messages.length === 0 ? (
                      <p className="py-12 text-center text-sm text-muted-foreground">Chưa có tin nào. Hãy bắt đầu câu hỏi.</p>
                    ) : (
                      <ul className="space-y-3">
                        {messages.map((m) => (
                          <li
                            key={m.id}
                            className={cn("flex gap-2.5 rounded-lg p-2", m.isMine && "bg-brand-wash/60")}
                          >
                            <Avatar size="sm" className="mt-0.5">
                              <AvatarFallback className="bg-brand-wash text-[10px] font-semibold text-brand-ink">
                                {initials(m.authorName)}
                              </AvatarFallback>
                            </Avatar>
                            <div className="min-w-0 flex-1">
                              <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                                <span className="text-sm font-semibold">{m.authorName}</span>
                                {m.authorRole ? (
                                  <Badge variant="neutral" className="h-4 px-1.5 text-[10px]">
                                    {roleLabel(m.authorRole)}
                                  </Badge>
                                ) : null}
                                <span className="text-[11px] text-muted-foreground">{formatTime(m.createdAt)}</span>
                              </div>
                              <p className="mt-0.5 whitespace-pre-wrap break-words text-sm">{m.content}</p>
                              {!m.isMine && (
                                <div className="mt-1">
                                  <ReportMessageControl
                                    isReported={reported.has(m.id)}
                                    onReport={async (reason) => {
                                      await communityApi.report(m.id, reason.trim() || "Vi phạm");
                                      setReported((prev) => new Set(prev).add(m.id));
                                      toast.add({ type: "success", title: "Đã gửi báo cáo" });
                                    }}
                                  />
                                </div>
                              )}
                            </div>
                          </li>
                        ))}
                      </ul>
                    )}
                    <div ref={bottomRef} />
                  </>
                )}
              </div>
              <div className="border-t p-3">
                {restriction ? (
                  <p className="text-sm text-destructive">{restriction}</p>
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
            <aside className="hidden min-h-0 border-l lg:block">{sidebar}</aside>
          </CardContent>
        </Card>
      </section>
    </ProtectedRoute>
  );
}
