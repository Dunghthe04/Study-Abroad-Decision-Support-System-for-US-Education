// ponytail: temporary mock fixtures & fetch interceptor for /audit/community UI redesign preview
import type { PagedResult, UserRole } from "@/types/api";
import type {
  CommunityMessageDto,
  CommunityPresenceDto,
  CommunityReportDto,
  CommunityRestrictionDto,
} from "@/types/community";
import { MOCK_USERS } from "@/components/audit-center/audit-mock-data";

export type CommunityAuditPageId = "community_chat" | "admin_forum";

export interface CommunityAuditCaseDef {
  id: string;
  label: string;
  description: string;
  defaultRole?: UserRole;
}

export interface CommunityAuditPageDef {
  id: CommunityAuditPageId;
  route: string;
  label: string;
  shortLabel: string;
  defaultRole: UserRole;
  cases: CommunityAuditCaseDef[];
}

export const AUDIT_COMMUNITY_PAGES: CommunityAuditPageDef[] = [
  {
    id: "community_chat",
    route: "/community",
    label: "Kênh thảo luận cộng đồng",
    shortLabel: "1. /community",
    defaultRole: "student",
    cases: [
      { id: "populated_active", label: "Đủ dữ liệu (6 online)", description: "Tin nhắn nhiều ngày, gộp nhóm tác giả, trích dẫn trả lời, link, ảnh đính kèm & danh sách online" },
      { id: "solo_room", label: "Ở trong phòng một mình", description: "Có lịch sử chat nhưng thanh bên báo 'Bạn đang ở trong phòng một mình'" },
      { id: "empty_room", label: "Phòng trống (0 tin, 0 người)", description: "Trạng thái chưa có tin nhắn nào & chưa có ai trong phòng" },
      { id: "restricted", label: "Bị hạn chế chat", description: "Ẩn khung nhập tin nhắn và hiện thông báo thời hạn + lý do bị hạn chế" },
      { id: "moderator_view", label: "Góc nhìn Kiểm duyệt viên", description: "Hover vào tin nhắn hiện thêm nút Ẩn tin nhắn (biểu tượng mắt gạch chéo)", defaultRole: "moderator" },
      { id: "load_error", label: "Lỗi tải phòng chat (500)", description: "API lỗi khi tải tin nhắn ban đầu, hiện toast báo lỗi" },
    ],
  },
  {
    id: "admin_forum",
    route: "/admin/forum",
    label: "Kiểm duyệt diễn đàn",
    shortLabel: "2. /admin/forum",
    defaultRole: "moderator",
    cases: [
      { id: "reports_open", label: "Có báo cáo đang mở (3)", description: "Báo cáo thường + tác giả đang bị hạn chế (nút Ẩn tin, Bỏ qua, Hạn chế / Gỡ hạn chế)" },
      { id: "reports_empty", label: "Không có báo cáo nào", description: "Trạng thái danh sách báo cáo trống" },
      { id: "reports_error", label: "Lỗi tải danh sách báo cáo", description: "API báo lỗi 500 khi tải hàng đợi kiểm duyệt" },
    ],
  },
];

const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString();
const daysAgo = (d: number, hourOffset = 0) => new Date(Date.now() - d * 86_400_000 + hourOffset * 3_600_000).toISOString();
const daysAhead = (d: number) => new Date(Date.now() + d * 86_400_000).toISOString();

export const MOCK_COMMUNITY_MEMBERS: CommunityPresenceDto[] = [
  { userId: "usr-student-1", fullName: "Nguyễn Gia Huy", role: "student", lastSeenAt: minutesAgo(0) },
  { userId: "usr-student-2", fullName: "Trần Minh Anh", role: "student", lastSeenAt: minutesAgo(1) },
  { userId: "usr-parent-1", fullName: "Nguyễn Văn Hùng", role: "parent", lastSeenAt: minutesAgo(4) },
  { userId: "usr-center-1", fullName: "Tư vấn Minh Khang Global", role: "center", lastSeenAt: minutesAgo(8) },
  { userId: "usr-mod-1", fullName: "Moderator Demo", role: "moderator", lastSeenAt: minutesAgo(2) },
  { userId: "usr-student-3", fullName: "Lê Hoàng Nam", role: "student", lastSeenAt: minutesAgo(19) },
];

export const MOCK_COMMUNITY_MESSAGES: CommunityMessageDto[] = [
  {
    id: "cmsg-1",
    userId: "usr-student-2",
    authorName: "Trần Minh Anh",
    authorRole: "student",
    content: "Chào mọi người, có bạn nào năm nay nộp Early Action cho Purdue với UMass Amherst không ạ?",
    createdAt: daysAgo(1, -2),
    isMine: false,
  },
  {
    id: "cmsg-2",
    userId: "usr-parent-1",
    authorName: "Nguyễn Văn Hùng",
    authorRole: "parent",
    content: "Nhà bác đang tìm hiểu chi phí bang Massachusetts và Indiana. Mọi người tham khảo bảng so sánh ở https://localhost:3000/costs nhé.",
    createdAt: daysAgo(1, -1),
    isMine: false,
    replyTo: {
      id: "cmsg-1",
      userId: "usr-student-2",
      authorName: "Trần Minh Anh",
      contentSnippet: "Chào mọi người, có bạn nào năm nay nộp Early Action cho Purdue với UMass Amherst không ạ?",
    },
  },
  {
    id: "cmsg-3",
    userId: "usr-student-1",
    authorName: "Nguyễn Gia Huy",
    authorRole: "student",
    content: "Em nộp Purdue EA rồi ạ, học phí bang Indiana khá dễ thở so với bờ Đông.",
    createdAt: minutesAgo(52),
    isMine: true,
  },
  {
    id: "cmsg-4",
    userId: "usr-student-1",
    authorName: "Nguyễn Gia Huy",
    authorRole: "student",
    content: "Em gửi ảnh khuôn viên MIT hôm vừa rồi đi campus tour ảo cho mọi người lấy động lực nè!",
    createdAt: minutesAgo(50), // cùng tác giả < 5 phút → gộp nhóm không lặp avatar
    isMine: true,
    imageUrl: "/schools/mit.jpg",
  },
  {
    id: "cmsg-5",
    userId: "usr-center-1",
    authorName: "Tư vấn Minh Khang Global",
    authorRole: "center",
    content: "Lưu ý nhỏ cho các bạn nộp EA tháng 11: nhớ kiểm tra hạn gửi bảng điểm chính thức và điểm IELTS/SAT trước deadline ít nhất 5 ngày nhé.",
    createdAt: minutesAgo(18),
    isMine: false,
  },
  {
    id: "cmsg-6",
    userId: "usr-student-3",
    authorName: "Lê Hoàng Nam",
    authorRole: "student",
    content: "Dạ em cảm ơn ạ! À mọi người ơi cho em hỏi CSS Profile có cần dịch công chứng sổ tiết kiệm không?",
    createdAt: minutesAgo(6),
    isMine: false,
    replyTo: {
      id: "cmsg-5",
      userId: "usr-center-1",
      authorName: "Tư vấn Minh Khang Global",
      contentSnippet: "Lưu ý nhỏ cho các bạn nộp EA tháng 11: nhớ kiểm tra hạn gửi bảng điểm chính thức...",
    },
  },
];

export const MOCK_COMMUNITY_REPORTS: CommunityReportDto[] = [
  {
    id: "crep-1",
    messageId: "cmsg-bad-1",
    messageContent: "Inbox zalo 0909xxxxxx để nhận dịch vụ làm giả chứng minh tài chính đậu visa 100% nhé các bạn!",
    messageCreatedAt: minutesAgo(45),
    authorUserId: "usr-spam-1",
    authorName: "Tài khoản Quảng Cáo Lạ",
    reporterUserId: "usr-student-2",
    reporterName: "Trần Minh Anh",
    reason: "Spam hoặc quảng cáo dịch vụ trái phép",
    detail: "Chào mời làm giả giấy tờ tài chính và kéo người dùng ra ngoài nền tảng.",
    status: "open",
    createdAt: minutesAgo(30),
    authorIsRestricted: false,
    authorRestrictedUntil: null,
  },
  {
    id: "crep-2",
    messageId: "cmsg-bad-2",
    messageContent: "Mấy câu hỏi cơ bản thế này mà cũng lên hỏi, đúng là phí thời gian.",
    messageCreatedAt: daysAgo(1),
    authorUserId: "usr-toxic-2",
    authorName: "Phạm Quốc Đạt",
    reporterUserId: "usr-parent-1",
    reporterName: "Nguyễn Văn Hùng",
    reason: "Ngôn từ thiếu tôn trọng, công kích",
    detail: null,
    status: "open",
    createdAt: minutesAgo(95),
    authorIsRestricted: true,
    authorRestrictedUntil: daysAhead(5),
  },
  {
    id: "crep-3",
    messageId: "cmsg-bad-3",
    messageContent: "Đây là số CCCD và địa chỉ nhà của bạn X ở quận Cầu Giấy...",
    messageCreatedAt: minutesAgo(120),
    authorUserId: "usr-dox-3",
    authorName: "Nguyễn Tùng",
    reporterUserId: "usr-student-3",
    reporterName: "Lê Hoàng Nam",
    reason: "Tiết lộ thông tin cá nhân người khác",
    detail: "Đăng số giấy tờ tùy thân công khai trong kênh chung.",
    status: "open",
    createdAt: minutesAgo(110),
    authorIsRestricted: false,
    authorRestrictedUntil: null,
  },
];

export interface CommunityAuditRuntimeConfig {
  pageId: CommunityAuditPageId;
  caseId: string;
  role: UserRole;
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === null ? "" : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

export function membersForCase(pageId: CommunityAuditPageId, caseId: string, role: UserRole): CommunityPresenceDto[] {
  if (pageId !== "community_chat") return [];
  if (caseId === "empty_room") return [];
  const me = MOCK_USERS[role] ?? MOCK_USERS.student!;
  if (caseId === "solo_room") {
    return [{ userId: me.id, fullName: me.fullName, role: me.role, lastSeenAt: minutesAgo(0) }];
  }
  const rest = MOCK_COMMUNITY_MEMBERS.filter((m) => m.userId !== me.id);
  return [{ userId: me.id, fullName: me.fullName, role: me.role, lastSeenAt: minutesAgo(0) }, ...rest];
}

export function installCommunityAuditFetchInterceptor(getConfig: () => CommunityAuditRuntimeConfig): () => void {
  if (typeof window === "undefined") return () => undefined;

  const originalFetch = window.fetch.bind(window);
  let localMessages = structuredClone(MOCK_COMMUNITY_MESSAGES);
  let localReports = structuredClone(MOCK_COMMUNITY_REPORTS);
  let lastCaseKey = "";

  window.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const rawUrl = typeof input === "string" ? input : input instanceof Request ? input.url : String(input);
    let url: URL;
    try {
      url = new URL(rawUrl, window.location.origin);
    } catch {
      return originalFetch(input, init);
    }

    if (!url.pathname.startsWith("/api/v1/")) {
      return originalFetch(input, init);
    }

    const { pageId, caseId, role } = getConfig();
    const method = (init?.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    const path = url.pathname;

    const currentKey = `${pageId}:${caseId}`;
    if (currentKey !== lastCaseKey) {
      lastCaseKey = currentKey;
      localMessages = structuredClone(MOCK_COMMUNITY_MESSAGES);
      localReports = structuredClone(MOCK_COMMUNITY_REPORTS);
    }

    // 1. Auth & shell chrome
    if (path === "/api/v1/auth/me") {
      return jsonResponse(MOCK_USERS[role] ?? MOCK_USERS.student);
    }
    if (path === "/api/v1/auth/acting") {
      return jsonResponse({ acting: false, childId: null, childName: null });
    }
    if (path === "/api/v1/policy-acceptance") {
      return jsonResponse({ mustAccept: false, hasUpdates: false, items: [] });
    }
    if (path === "/api/v1/notifications/unread-count") {
      return jsonResponse({ unreadCount: 0 });
    }
    if (path === "/api/v1/notifications") {
      return jsonResponse({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 1 });
    }
    if (path === "/api/v1/subscription") {
      return jsonResponse({ tier: "free", status: "active" });
    }
    if (path === "/api/v1/subscription/quota") {
      return jsonResponse({ used: 1, limit: 5 });
    }
    if (path === "/api/v1/parent-links/me") {
      return jsonResponse({ items: [] });
    }
    if (path === "/api/v1/leads/sent/conversations" || path === "/api/v1/leads/received/conversations") {
      return jsonResponse({ totalUnread: 0, items: [] });
    }

    // 2. Community Chat APIs
    if (path === "/api/v1/community/messages") {
      if (method === "GET") {
        if (pageId === "community_chat" && caseId === "load_error") {
          return jsonResponse({ message: "Không thể tải phòng chat (mô phỏng lỗi 500)." }, 500);
        }
        if (pageId === "community_chat" && caseId === "empty_room") {
          return jsonResponse([]);
        }
        // Backend returns newest-first (CommunityChatView reverses on initial load)
        const after = url.searchParams.get("after");
        const before = url.searchParams.get("before");
        if (after || before) {
          return jsonResponse([]);
        }
        return jsonResponse([...localMessages].reverse());
      }
      if (method === "POST") {
        const parsed = init?.body ? (JSON.parse(String(init.body)) as { content?: string; replyToId?: string | null }) : {};
        const me = MOCK_USERS[role] ?? MOCK_USERS.student!;
        const replyTarget = parsed.replyToId ? localMessages.find((m) => m.id === parsed.replyToId) : null;
        const created: CommunityMessageDto = {
          id: `cmsg-${Date.now()}`,
          userId: me.id,
          authorName: me.fullName,
          authorRole: me.role,
          content: parsed.content ?? "",
          createdAt: new Date().toISOString(),
          isMine: true,
          replyTo: replyTarget
            ? {
                id: replyTarget.id,
                userId: replyTarget.userId,
                authorName: replyTarget.authorName,
                contentSnippet: replyTarget.content || "[Hình ảnh]",
              }
            : null,
        };
        localMessages.push(created);
        return jsonResponse(created, 201);
      }
    }

    if (path === "/api/v1/community/messages/image" && method === "POST") {
      const me = MOCK_USERS[role] ?? MOCK_USERS.student!;
      const created: CommunityMessageDto = {
        id: `cmsg-${Date.now()}`,
        userId: me.id,
        authorName: me.fullName,
        authorRole: me.role,
        content: "(Ảnh gửi từ chế độ xem thử)",
        imageUrl: "/schools/stanford.jpg",
        createdAt: new Date().toISOString(),
        isMine: true,
      };
      localMessages.push(created);
      return jsonResponse(created, 201);
    }

    if (/^\/api\/v1\/community\/messages\/[^/]+\/report$/.test(path) && method === "POST") {
      return new Response(null, { status: 204 });
    }

    if (path === "/api/v1/community/restriction") {
      if (pageId === "community_chat" && caseId === "restricted") {
        return jsonResponse({
          isRestricted: true,
          restrictedUntil: daysAhead(7),
          reason: "Gửi tin nhắn quảng cáo ngoài phạm vi thảo luận cộng đồng.",
          message: "Tài khoản đang bị hạn chế chat.",
        } satisfies CommunityRestrictionDto);
      }
      return jsonResponse({
        isRestricted: false,
        restrictedUntil: null,
        reason: null,
        message: null,
      } satisfies CommunityRestrictionDto);
    }

    // 3. Admin Forum Moderation APIs
    if (path === "/api/v1/admin/forum/reports") {
      if (pageId === "admin_forum" && caseId === "reports_error") {
        return jsonResponse({ message: "Không tải được danh sách báo cáo (mô phỏng lỗi 500)." }, 500);
      }
      if (pageId === "admin_forum" && caseId === "reports_empty") {
        return jsonResponse({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 1 } satisfies PagedResult<CommunityReportDto>);
      }
      return jsonResponse({
        items: localReports,
        page: 1,
        pageSize: 50,
        totalCount: localReports.length,
        totalPages: 1,
      } satisfies PagedResult<CommunityReportDto>);
    }

    const reportActionMatch = path.match(/^\/api\/v1\/admin\/forum\/reports\/([^/]+)\/(hide|dismiss)$/);
    if (reportActionMatch && method === "POST") {
      const id = decodeURIComponent(reportActionMatch[1]);
      localReports = localReports.filter((r) => r.id !== id);
      return jsonResponse({ message: "Đã xử lý báo cáo (mô phỏng)." });
    }

    const hideMsgMatch = path.match(/^\/api\/v1\/admin\/forum\/messages\/([^/]+)\/hide$/);
    if (hideMsgMatch && method === "POST") {
      const id = decodeURIComponent(hideMsgMatch[1]);
      localMessages = localMessages.filter((m) => m.id !== id);
      return jsonResponse({ message: "Đã ẩn tin nhắn (mô phỏng)." });
    }

    const restrictMatch = path.match(/^\/api\/v1\/admin\/forum\/users\/([^/]+)\/(restrict|unrestrict)$/);
    if (restrictMatch && method === "POST") {
      const userId = decodeURIComponent(restrictMatch[1]);
      const action = restrictMatch[2];
      localReports = localReports.map((r) =>
        r.authorUserId === userId
          ? {
              ...r,
              authorIsRestricted: action === "restrict",
              authorRestrictedUntil: action === "restrict" ? daysAhead(7) : null,
            }
          : r,
      );
      return jsonResponse({ message: "Đã cập nhật trạng thái hạn chế (mô phỏng)." });
    }

    return jsonResponse({});
  };

  return () => {
    window.fetch = originalFetch;
  };
}
