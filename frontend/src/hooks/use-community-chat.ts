"use client";

import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { invokeRealtime, isRealtimeConnected, onRealtime, subscribeRealtimeState } from "@/lib/realtime";
import type { CommunityMessageDto, CommunityPresenceDto, CommunityRestrictionDto, JoinCommunityResult } from "@/types/community";

const FALLBACK_POLL_MS = 20_000;

interface HiddenPayload {
  id: string;
}

interface PresencePayload {
  joined: boolean;
  member: CommunityPresenceDto;
}

export function useCommunityChat(options: {
  enabled?: boolean;
  onNewMessage: (message: CommunityMessageDto) => void;
  onHidden: (id: string) => void;
  onResync: () => void;
  onMembers: (members: CommunityPresenceDto[]) => void;
  onMemberChange: (joined: boolean, member: CommunityPresenceDto) => void;
  onRestrictionChanged?: (payload: CommunityRestrictionDto) => void;
}) {
  const { enabled = true } = options;
  const callbacks = useRef(options);
  useEffect(() => {
    callbacks.current = options;
  });

  const connected = useSyncExternalStore(subscribeRealtimeState, isRealtimeConnected, () => false);
  const active = enabled;
  const joined = useRef(false);

  const [roomConnected, setRoomConnected] = useState(false);

  // Khi chuyển tab quay lại, gọi onResync để tải bù tin nhắn mới nhất
  useEffect(() => {
    if (!active) return;
    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        callbacks.current.onResync();
      }
    };
    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => document.removeEventListener("visibilitychange", onVisibilityChange);
  }, [active]);

  useEffect(() => {
    if (!active) return;

    const join = async () => {
      try {
        const result = await invokeRealtime<JoinCommunityResult>("JoinCommunity");
        joined.current = true;
        setRoomConnected(true);
        callbacks.current.onMembers(result.members ?? []);
      } catch {
        joined.current = false;
        setRoomConnected(false);
      }
    };

    if (isRealtimeConnected()) void join();

    const offConnected = onRealtime("connected", () => void join());
    const offReconnected = onRealtime("reconnected", () => {
      void join();
      callbacks.current.onResync();
    });
    const offDisconnected = onRealtime("disconnected", () => {
      joined.current = false;
      setRoomConnected(false);
    });
    const offNew = onRealtime<CommunityMessageDto>("newCommunityMessage", (message) => {
      callbacks.current.onNewMessage(message);
    });
    const offHidden = onRealtime<HiddenPayload>("communityMessageHidden", (payload) => {
      if (payload?.id) callbacks.current.onHidden(payload.id);
    });
    const offPresence = onRealtime<PresencePayload>("communityPresence", (payload) => {
      if (payload?.member) callbacks.current.onMemberChange(payload.joined, payload.member);
    });
    const offRestriction = onRealtime<CommunityRestrictionDto>("communityRestrictionChanged", (payload) => {
      if (payload && callbacks.current.onRestrictionChanged) {
        callbacks.current.onRestrictionChanged(payload);
      }
    });

    return () => {
      offConnected();
      offReconnected();
      offDisconnected();
      offNew();
      offHidden();
      offPresence();
      offRestriction();
      if (joined.current) void invokeRealtime("LeaveCommunity").catch(() => undefined);
      joined.current = false;
    };
  }, [active]);

  useEffect(() => {
    if (!active || connected) return;
    const timer = setInterval(() => callbacks.current.onResync(), FALLBACK_POLL_MS);
    return () => clearInterval(timer);
  }, [active, connected]);

  return { connected: connected && roomConnected };
}
