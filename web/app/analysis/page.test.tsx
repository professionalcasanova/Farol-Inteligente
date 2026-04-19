import type { ReactNode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AnalysisPage from "@/app/analysis/page";
import { ApiError, getMonthHealth } from "@/lib/api";
import { useProtectedSession } from "@/lib/use-protected-session";

vi.mock("@/components/app-shell", () => ({
  AppShell: ({
    title,
    description,
    actions,
    children,
  }: {
    title: string;
    description: string;
    actions?: ReactNode;
    children: ReactNode;
  }) => (
    <div>
      <h1>{title}</h1>
      <p>{description}</p>
      {actions}
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
    getMonthHealth: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedGetMonthHealth = vi.mocked(getMonthHealth);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("AnalysisPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedGetMonthHealth.mockReset();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    window.history.replaceState({}, "", "/analysis?month=2026-03");
  });

  it("Analysis_Load_ShowsMonthHealthDetails", async () => {
    mockedGetMonthHealth.mockResolvedValue({
      status: "attention",
      score: 82,
      reasons: [
        "Existe uma conta vencida com impacto moderado no mes.",
        "O valor atrasado ainda cabe no saldo, mas pede correcao.",
      ],
      actions: [
        "Quite ou renegocie a conta vencida primeiro.",
        "Revise os proximos vencimentos antes de novos gastos.",
      ],
      summary: {
        message: "Seu mes pede atencao em um ponto especifico.",
        cause: "Existe um atraso que ainda nao desorganiza o mes, mas merece correcao.",
        action: "Resolva primeiro a conta vencida para evitar juros e manter o controle.",
      },
      recommendedActions: [
        {
          id: "review_overdue_bills",
          label: "Ver contas vencidas",
          target: "/bills?status=overdue",
        },
      ],
      insights: [
        {
          type: "overdue_bills",
          severity: "medium",
          priority: 70,
          message: "Voce tem contas vencidas que pedem atencao.",
          cause: "As contas vencidas somam 220.00 e ja merecem atencao.",
          action: "Priorize quitar ou renegociar as contas vencidas primeiro.",
        },
      ],
    });

    render(<AnalysisPage />);

    expect(await screen.findByText("Seu mes pede atencao em um ponto especifico.")).toBeInTheDocument();
    expect(screen.getByText("Mes analisado")).toBeInTheDocument();
    expect(screen.getByText("Razoes principais")).toBeInTheDocument();
    expect(screen.getByTestId("analysis-reasons-list")).toBeInTheDocument();
    expect(screen.getByTestId("analysis-actions-list")).toBeInTheDocument();
    expect(screen.getByTestId("analysis-insights-list")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ver contas vencidas" })).toHaveAttribute(
      "href",
      "/bills?status=overdue",
    );
    expect(screen.getByRole("link", { name: "Voltar ao dashboard" })).toHaveAttribute(
      "href",
      "/dashboard?month=2026-03",
    );
  });

  it("Analysis_LoadError_ShowsReadableState", async () => {
    mockedGetMonthHealth.mockRejectedValue(
      new ApiError(
        "Npgsql.PostgresException: relation \"month_health\" does not exist",
        500,
      ),
    );

    render(<AnalysisPage />);

    expect(await screen.findByText("Falha ao carregar")).toBeInTheDocument();
    expect(screen.getByText("Nao foi possivel abrir a analise do mes")).toBeInTheDocument();
    expect(
      screen.getByText(
        "Nao foi possivel carregar a analise do mes agora. Tente novamente em alguns instantes.",
      ),
    ).toBeInTheDocument();
  });

  it("Analysis_Load_RequestsCurrentMonthFromQueryString", async () => {
    mockedGetMonthHealth.mockResolvedValue({
      status: "healthy",
      summary: {
        message: "Seu mes esta sob controle ate aqui.",
        cause: "Voce nao tem sinais fortes de pressao imediata.",
        action: "Continue acompanhando o mes.",
      },
      insights: [],
    });

    render(<AnalysisPage />);

    await waitFor(() => {
      expect(mockedGetMonthHealth).toHaveBeenCalledWith("token", 3, 2026);
    });
  });
});
