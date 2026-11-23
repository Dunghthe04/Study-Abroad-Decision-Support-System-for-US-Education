import type { UserRole } from "@/types/api";

export interface CommunityReplyToDto {
  id: string;
  userId: string | null;
  authorName: string;
  contentSnippet: string;
}

export interface CommunityMessageDto {
  id: string;
  userId: string | null;
  authorName: string;
  authorRole: UserRole | "";
  content: string;
  createdAt: string;
  isMine: boolean;
  imageUrl?: string | null;
  replyTo?: CommunityReplyToDto | null;
}

export interface CommunityPresenceDto {
  userId: string;
  fullName: string;
  role: UserRole | "";
  lastSeenAt?: string | null;
}

export interface JoinCommunityResult {
  members: CommunityPresenceDto[];
}

export interface CommunityRestrictionDto {
  isRestricted: boolean;
  restrictedUntil: string | null;
  reason: string | null;
  message: string | null;
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
  authorIsRestricted?: boolean;
  authorRestrictedUntil?: string | null;
}
