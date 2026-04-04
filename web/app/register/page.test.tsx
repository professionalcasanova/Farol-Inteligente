import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import RegisterPage from "@/app/register/page";
import { readStoredSession, type StoredSession } from "@/lib/auth";
import { ApiError, register } from "@/lib/api";

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
    register: vi.fn(),
  };
});

const mockedRegister = vi.mocked(register);

const session: StoredSession = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria Silva",
  email: "maria@email.com",
};

describe("RegisterPage", () => {
  beforeEach(() => {
    replace.mockReset();
    mockedRegister.mockReset();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  async function fillAndSubmit() {
    const user = userEvent.setup();

    await user.type(
      await screen.findByRole("textbox", { name: /nome/i }),
      "Maria Silva",
    );
    await user.type(
      screen.getByRole("textbox", { name: /e-mail/i }),
      "maria@email.com",
    );
    await user.type(screen.getByLabelText(/senha/i), "123456");
    await user.click(screen.getByRole("button", { name: /criar conta/i }));
  }

  it("Register_SubmitValidData_SavesSessionAndRedirects", async () => {
    mockedRegister.mockResolvedValue(session);

    render(<RegisterPage />);

    await fillAndSubmit();

    await waitFor(() => {
      expect(readStoredSession()).toEqual(session);
      expect(replace).toHaveBeenCalledWith("/dashboard");
    });
  });

  it("Register_DuplicateEmail_ShowsFriendlyError", async () => {
    mockedRegister.mockRejectedValue(new ApiError("Email is already in use.", 409));

    render(<RegisterPage />);

    await fillAndSubmit();

    expect(
      await screen.findByText(
        "Ja existe uma conta com esse e-mail. Tente entrar ou use outro endereco.",
      ),
    ).toBeInTheDocument();
  });

  it("Register_SubmitWithoutRequiredFields_ShowsValidationMessage", async () => {
    const user = userEvent.setup();

    render(<RegisterPage />);

    await user.click(await screen.findByRole("button", { name: /criar conta/i }));

    expect(
      await screen.findByText(
        "Preencha nome, e-mail e senha para criar sua conta.",
      ),
    ).toBeInTheDocument();

    expect(mockedRegister).not.toHaveBeenCalled();
  });
});
