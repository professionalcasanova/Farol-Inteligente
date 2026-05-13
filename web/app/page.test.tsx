import { render, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import HomePage from "@/app/page";
import { clearStoredSession, writeStoredSession, type StoredSession } from "@/lib/auth";
import { refreshSession } from "@/lib/api";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    replace,
  }),
}));

vi.mock("@/lib/api", () => ({
  refreshSession: vi.fn(),
}));

const mockedRefreshSession = vi.mocked(refreshSession);

const session: StoredSession = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("HomePage", () => {
  beforeEach(() => {
    replace.mockReset();
    mockedRefreshSession.mockReset();
    mockedRefreshSession.mockRejectedValue(new Error("missing refresh cookie"));
    clearStoredSession();
    window.localStorage.clear();
  });

  it("Home_WithoutSession_RedirectsToLogin", async () => {
    render(<HomePage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/login");
    });
  });

  it("Home_WithSession_RedirectsToDashboard", async () => {
    writeStoredSession(session);

    render(<HomePage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/dashboard");
    });
  });
});
