import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  consumeAuthNotice,
  clearStoredSession,
  readStoredSession,
  writeStoredSession,
  type StoredSession,
} from "@/lib/auth";
import {
  logoutProtectedSession,
  resolveProtectedSession,
} from "@/lib/use-protected-session";

const session: StoredSession = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("protected session helpers", () => {
  beforeEach(() => {
    clearStoredSession();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it("UseProtectedSession_WithoutToken_RedirectsToLogin", () => {
    const router = {
      replace: vi.fn(),
    };

    const storedSession = resolveProtectedSession(router);

    expect(storedSession).toBeNull();
    expect(router.replace).toHaveBeenCalledWith("/login");
  });

  it("UseProtectedSession_WithToken_AllowsRendering", () => {
    const router = {
      replace: vi.fn(),
    };

    writeStoredSession(session);

    const storedSession = resolveProtectedSession(router);

    expect(storedSession).toEqual(session);
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("Logout_ClearsSessionData", () => {
    const router = {
      replace: vi.fn(),
    };

    writeStoredSession(session);

    logoutProtectedSession(router);

    expect(readStoredSession()).toBeNull();
    expect(router.replace).toHaveBeenCalledWith("/login");
  });

  it("Logout_WhenSessionExpired_PersistsSingleUseNotice", () => {
    const router = {
      replace: vi.fn(),
    };

    writeStoredSession(session);

    logoutProtectedSession(router, "session-expired");

    expect(readStoredSession()).toBeNull();
    expect(consumeAuthNotice()).toBe("session-expired");
    expect(consumeAuthNotice()).toBeNull();
    expect(router.replace).toHaveBeenCalledWith("/login");
  });
});
