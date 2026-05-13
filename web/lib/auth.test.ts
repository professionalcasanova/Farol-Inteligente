import { beforeEach, describe, expect, it } from "vitest";
import {
  consumeAuthNotice,
  clearStoredSession,
  readStoredSession,
  writeAuthNotice,
  writeStoredSession,
  type StoredSession,
} from "@/lib/auth";

const session: StoredSession = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("auth storage", () => {
  beforeEach(() => {
    clearStoredSession();
    window.sessionStorage.clear();
  });

  it("ReadStoredSession_WithoutMemorySession_ReturnsNull", () => {
    clearStoredSession();

    expect(readStoredSession()).toBeNull();
  });

  it("WriteStoredSession_ValidSession_KeepsSessionInMemoryOnly", () => {
    writeStoredSession(session);

    expect(readStoredSession()).toEqual(session);
    expect(window.localStorage.getItem("farol.session")).toBeNull();
  });

  it("ClearStoredSession_WithPersistedSession_RemovesSession", () => {
    writeStoredSession(session);

    clearStoredSession();

    expect(readStoredSession()).toBeNull();
  });

  it("ConsumeAuthNotice_WithPersistedNotice_ReturnsAndClearsNotice", () => {
    writeAuthNotice("session-expired");

    expect(consumeAuthNotice()).toBe("session-expired");
    expect(consumeAuthNotice()).toBeNull();
  });
});
