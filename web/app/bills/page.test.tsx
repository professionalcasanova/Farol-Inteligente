import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import BillsPage from "@/app/bills/page";
import {
  createBill,
  listBills,
  payBill,
  unpayBill,
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
    createBill: vi.fn(),
    listBills: vi.fn(),
    payBill: vi.fn(),
    unpayBill: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedCreateBill = vi.mocked(createBill);
const mockedListBills = vi.mocked(listBills);
const mockedPayBill = vi.mocked(payBill);
const mockedUnpayBill = vi.mocked(unpayBill);

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Maria",
  email: "maria@email.com",
};

describe("BillsPage", () => {
  beforeEach(() => {
    mockedUseProtectedSession.mockReset();
    mockedCreateBill.mockReset();
    mockedListBills.mockReset();
    mockedPayBill.mockReset();
    mockedUnpayBill.mockReset();
  });

  it("Bills_CreateInstallment_SendsInstallmentPayload", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListBills.mockResolvedValue([]);
    mockedCreateBill.mockResolvedValue({
      id: "bill-1",
      description: "Notebook",
      amount: 320,
      dueOn: "2026-03-08",
      billSeriesId: "series-1",
      seriesKind: "installment",
      occurrenceNumber: 1,
      totalOccurrences: 12,
      isPaid: false,
      paidAtUtc: null,
      createdAtUtc: "2026-03-01T00:00:00Z",
      status: "pending",
    });

    render(<BillsPage />);

    await user.selectOptions(
      await screen.findByLabelText("Tipo de conta"),
      "installment",
    );
    await user.type(screen.getByLabelText("Descricao"), "Notebook");
    await user.type(screen.getByLabelText("Valor"), "320");
    await user.clear(screen.getByLabelText("Vencimento"));
    await user.type(screen.getByLabelText("Vencimento"), "2026-03-08");
    await user.clear(screen.getByLabelText("Total de parcelas"));
    await user.type(screen.getByLabelText("Total de parcelas"), "12");
    await user.click(screen.getByRole("button", { name: /criar conta a pagar/i }));

    await waitFor(() => {
      expect(mockedCreateBill).toHaveBeenCalledWith("token", {
        description: "Notebook",
        amount: 320,
        dueOn: "2026-03-08",
        recurrence: {
          kind: "installment",
          frequency: "monthly",
          endMode: "occurrence_count",
          occurrenceCount: 12,
        },
      });
    });
  });

  it("Bills_List_ShowsBillKindsAndInstallmentProgress", async () => {
    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListBills.mockResolvedValue([
      {
        id: "bill-1",
        description: "Internet",
        amount: 99.9,
        dueOn: "2026-03-10",
        billSeriesId: null,
        seriesKind: null,
        occurrenceNumber: null,
        totalOccurrences: null,
        isPaid: false,
        paidAtUtc: null,
        createdAtUtc: "2026-03-01T00:00:00Z",
        status: "pending",
      },
      {
        id: "bill-2",
        description: "Academia",
        amount: 120,
        dueOn: "2026-03-12",
        billSeriesId: "series-2",
        seriesKind: "recurring",
        occurrenceNumber: 2,
        totalOccurrences: 6,
        isPaid: false,
        paidAtUtc: null,
        createdAtUtc: "2026-03-01T00:00:00Z",
        status: "pending",
      },
      {
        id: "bill-3",
        description: "Notebook",
        amount: 320,
        dueOn: "2026-03-08",
        billSeriesId: "series-3",
        seriesKind: "installment",
        occurrenceNumber: 3,
        totalOccurrences: 12,
        isPaid: false,
        paidAtUtc: null,
        createdAtUtc: "2026-03-01T00:00:00Z",
        status: "pending",
      },
    ]);

    render(<BillsPage />);

    const notebookCard = await screen.findByText("Notebook");
    const agendaSection = notebookCard.closest("article")?.parentElement;

    expect(await screen.findByText("Avulsa")).toBeInTheDocument();
    expect(screen.getByText("Recorrente")).toBeInTheDocument();
    expect(agendaSection).not.toBeNull();

    const scoped = within(agendaSection!);

    expect(scoped.getByText("Parcelada")).toBeInTheDocument();
    expect(scoped.getByText("Parcela 3/12")).toBeInTheDocument();
  });
});
