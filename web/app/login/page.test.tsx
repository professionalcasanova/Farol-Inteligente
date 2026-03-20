import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import LoginPage from "@/app/login/page";
import { readStoredSession, writeStoredSession, type StoredSession } from "@/lib/auth";
import { ApiError, login } from "@/lib/api";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    replace,
  }),
}));

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    login: vi.fn(),
  };
});

const mockedLogin = vi.mocked(login);

const session: StoredSession = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("LoginPage", () => {
  beforeEach(() => {
    replace.mockReset();
    mockedLogin.mockReset();
    window.localStorage.clear();
  });

  async function fillAndSubmit() {
    const user = userEvent.setup();

    await user.type(await screen.findByRole("textbox", { name: /email/i }), "maria@email.com");
    await user.type(screen.getByLabelText(/senha/i), "123456");
    await user.click(screen.getByRole("button", { name: /entrar no farol/i }));
  }

  it("Login_SubmitValidCredentials_SavesSession", async () => {
    mockedLogin.mockResolvedValue(session);

    render(<LoginPage />);

    await fillAndSubmit();

    await waitFor(() => {
      expect(readStoredSession()).toEqual(session);
    });
  });

  it("Login_SubmitValidCredentials_RedirectsToDashboard", async () => {
    mockedLogin.mockResolvedValue(session);

    render(<LoginPage />);

    await fillAndSubmit();

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/dashboard");
    });
  });

  it("Login_SubmitInvalidCredentials_ShowsFriendlyError", async () => {
    mockedLogin.mockRejectedValue(new ApiError("Email ou senha invalidos.", 401));

    render(<LoginPage />);

    await fillAndSubmit();

    expect(await screen.findByText("Email ou senha invalidos.")).toBeInTheDocument();
  });

  it("Login_WhenApiReturnsUnexpectedError_ShowsGenericFriendlyError", async () => {
    mockedLogin.mockRejectedValue(new Error("boom"));

    render(<LoginPage />);

    await fillAndSubmit();

    expect(await screen.findByText("Nao foi possivel entrar agora.")).toBeInTheDocument();
  });

  it("Login_WhenAlreadyAuthenticated_RedirectsToDashboard", async () => {
    writeStoredSession(session);

    render(<LoginPage />);

    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/dashboard");
    });
  });
});
