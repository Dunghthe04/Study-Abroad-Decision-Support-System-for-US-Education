import type { UserRole } from "@/types/api";

export interface CommunityMessageDto {
  id: string;
  userId: string | null;
  authorName: string;
  authorRole: UserRole | "";
  content: string;
  createdAt: string;
  isMine: boolean;
}

export interface CommunityPresenceDto {
  userId: string;
  fullName: string;
  role: UserRole | "";
}

export interface JoinCommunityResult {
  members: CommunityPresenceDto[];
}

export interface CommunityReportDto {
  id: string;
  messageId: string;
  messageContent: string;
  messageCreatedAt: string;
  authorUserId: string | null;
  authorName: string;
  reporterUserId: string | null;
  reporterName: string;
  reason: string;
  detail: string | null;
  status: string;
  createdAt: string;
}
