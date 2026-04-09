import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import BudgetPage from "@/app/budget/page";
import {
  applyBudgetTemplate,
  getBudgetTemplate,
  getMonthlyBudget,
  listCategories,
  saveBudgetTemplate,
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
    applyBudgetTemplate: vi.fn(),
    getBudgetTemplate: vi.fn(),
    getMonthlyBudget: vi.fn(),
    listCategories: vi.fn(),
    saveBudgetTemplate: vi.fn(),
    saveMonthlyBudget: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedApplyBudgetTemplate = vi.mocked(applyBudgetTemplate);
const mockedGetBudgetTemplate = vi.mocked(getBudgetTemplate);
const mockedGetMonthlyBudget = vi.mocked(getMonthlyBudget);
const mockedListCategories = vi.mocked(listCategories);
const mockedSaveBudgetTemplate = vi.mocked(saveBudgetTemplate);
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
    mockedApplyBudgetTemplate.mockReset();
    mockedGetBudgetTemplate.mockReset();
    mockedGetMonthlyBudget.mockReset();
    mockedListCategories.mockReset();
    mockedSaveBudgetTemplate.mockReset();
    mockedSaveMonthlyBudget.mockReset();
  });

  function mockBudgetApi() {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListCategories.mockResolvedValue([
      {
        id: "category-1",
        name: "Alimentacao",
        type: 2,
        isSystem: true,
      },
      {
        id: "category-2",
        name: "Moradia",
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

    mockedGetBudgetTemplate.mockResolvedValue({
      totalPlanned: 0,
      categories: [],
    });
  }

  it("Budget_SaveWithEmptyRows_ShowsBudgetClearedMessage", async () => {
    const user = userEvent.setup();

    mockBudgetApi();

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
      await screen.findByRole("button", { name: /salvar orcamento mensal/i }),
    );

    expect(await screen.findByText(/limpo com sucesso/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(mockedSaveMonthlyBudget).toHaveBeenCalledTimes(1);
    });
  });

  it("Budget_SaveTemplate_PersistsRecurringTemplate", async () => {
    const user = userEvent.setup();

    mockBudgetApi();

    mockedSaveBudgetTemplate.mockResolvedValue({
      totalPlanned: 900,
      categories: [
        {
          categoryId: "category-2",
          categoryName: "Moradia",
          planned: 900,
        },
      ],
    });

    render(<BudgetPage />);

    const templateHeading = await screen.findByText("Base para os proximos meses");
    const templateSection = templateHeading.closest("section");
    expect(templateSection).not.toBeNull();

    const withinTemplate = templateSection!;
    const templateSelect = withinTemplate.querySelectorAll("select")[0] as HTMLSelectElement;
    const templateAmount = withinTemplate.querySelectorAll("input[type='number']")[0] as HTMLInputElement;

    await user.selectOptions(templateSelect, "category-2");
    await user.clear(templateAmount);
    await user.type(templateAmount, "900");
    await user.click(
      screen.getByRole("button", { name: /salvar template recorrente/i }),
    );

    await waitFor(() => {
      expect(mockedSaveBudgetTemplate).toHaveBeenCalledWith("token", [
        {
          categoryId: "category-2",
          planned: 900,
        },
      ]);
    });
  });

  it("Budget_ApplyTemplate_UsesTemplateForCurrentMonth", async () => {
    const user = userEvent.setup();

    mockBudgetApi();

    mockedGetBudgetTemplate.mockResolvedValue({
      totalPlanned: 1500,
      categories: [
        {
          categoryId: "category-2",
          categoryName: "Moradia",
          planned: 1200,
        },
        {
          categoryId: "category-1",
          categoryName: "Alimentacao",
          planned: 300,
        },
      ],
    });

    mockedApplyBudgetTemplate.mockResolvedValue({
      month: 3,
      year: 2026,
      totalPlanned: 1500,
      totalSpent: 0,
      totalRemaining: 1500,
      categories: [
        {
          categoryId: "category-2",
          categoryName: "Moradia",
          planned: 1200,
          spent: 0,
          remaining: 1200,
        },
        {
          categoryId: "category-1",
          categoryName: "Alimentacao",
          planned: 300,
          spent: 0,
          remaining: 300,
        },
      ],
    });

    render(<BudgetPage />);

    await user.click(await screen.findByRole("button", { name: /aplicar ao mes/i }));

    await waitFor(() => {
      expect(mockedApplyBudgetTemplate).toHaveBeenCalledWith("token", {
        month: expect.any(Number),
        year: 2026,
      });
    });

    expect(
      await screen.findByText(/template aplicado ao mes com sucesso/i),
    ).toBeInTheDocument();
  });
});
