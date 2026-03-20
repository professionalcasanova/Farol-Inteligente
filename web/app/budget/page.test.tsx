import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import BudgetPage from "@/app/budget/page";
import {
  getMonthlyBudget,
  listCategories,
  saveMonthlyBudget,
} from "@/lib/api";
import { useProtectedSession } from "@/lib/use-protected-session";

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: ComponentProps<"a">) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/components/app-shell", () => ({
  AppShell: ({
    title,
    description,
    children,
  }: {
    title: string;
    description: string;
    children: ReactNode;
  }) => (
    <div>
      <h1>{title}</h1>
      <p>{description}</p>
      {children}
    </div>
  ),
}));

vi.mock("@/components/month-picker", () => ({
  MonthPicker: ({ label }: { label: string }) => <div>{label}</div>,
}));

vi.mock("@/lib/use-protected-session", () => ({
  useProtectedSession: vi.fn(),
}));

vi.mock("@/lib/api", async () => {
  const actual = await vi.importActual<typeof import("@/lib/api")>("@/lib/api");

  return {
    ...actual,
    getMonthlyBudget: vi.fn(),
    listCategories: vi.fn(),
    saveMonthlyBudget: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedGetMonthlyBudget = vi.mocked(getMonthlyBudget);
const mockedListCategories = vi.mocked(listCategories);
const mockedSaveMonthlyBudget = vi.mocked(saveMonthlyBudget);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("BudgetPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedGetMonthlyBudget.mockReset();
    mockedListCategories.mockReset();
    mockedSaveMonthlyBudget.mockReset();
  });

  it("Budget_SaveWithEmptyRows_ShowsBudgetClearedMessage", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });
    mockedListCategories.mockResolvedValue([
      {
        id: "category-1",
        name: "Alimentação",
        type: 2,
        isSystem: true,
      },
    ]);
    mockedGetMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });
    mockedSaveMonthlyBudget.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 0,
      totalSpent: 0,
      totalRemaining: 0,
      categories: [],
    });

    render(<BudgetPage />);

    await user.click(
      await screen.findByRole("button", { name: /salvar orçamento mensal/i }),
    );

    expect(
      await screen.findByText(
        "Orçamento do mês limpo com sucesso. Você pode montar um novo planejamento quando quiser.",
      ),
    ).toBeInTheDocument();

    await waitFor(() => {
      expect(mockedSaveMonthlyBudget).toHaveBeenCalledTimes(1);
    });
  });
});
