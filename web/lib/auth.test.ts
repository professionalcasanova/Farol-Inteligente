import { describe, expect, it } from "vitest";
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
  it("ReadStoredSession_InvalidShape_ReturnsNull", () => {
    window.localStorage.setItem(
      "farol.session",
      JSON.stringify({
        accessToken: "token",
        email: "maria@email.com",
      }),
    );

    const storedSession = readStoredSession();

    expect(storedSession).toBeNull();
    expect(window.localStorage.getItem("farol.session")).toBeNull();
  });

  it("WriteStoredSession_ValidSession_PersistsSession", () => {
    writeStoredSession(session);

    expect(readStoredSession()).toEqual(session);
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
