import type { ComponentProps, ReactNode } from "react";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import BillsPage from "@/app/bills/page";
import {
  createBill,
  deleteBill,
  listBills,
  payBill,
  unpayBill,
  updateBill,
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
    deleteBill: vi.fn(),
    listBills: vi.fn(),
    payBill: vi.fn(),
    unpayBill: vi.fn(),
    updateBill: vi.fn(),
  };
});

const mockedUseProtectedSession = vi.mocked(useProtectedSession);
const mockedCreateBill = vi.mocked(createBill);
const mockedDeleteBill = vi.mocked(deleteBill);
const mockedListBills = vi.mocked(listBills);
const mockedPayBill = vi.mocked(payBill);
const mockedUnpayBill = vi.mocked(unpayBill);
const mockedUpdateBill = vi.mocked(updateBill);

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
    mockedDeleteBill.mockReset();
    mockedListBills.mockReset();
    mockedPayBill.mockReset();
    mockedUnpayBill.mockReset();
    mockedUpdateBill.mockReset();
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
    expect(screen.getAllByRole("button", { name: /editar ocorrencia/i })).toHaveLength(2);
    expect(
      screen.getByText(/editar e pagar atuam na ocorrencia atual/i),
    ).toBeInTheDocument();
  });

  it("Bills_EditSingleBill_UsesUpdateFlow", async () => {
    const user = userEvent.setup();

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
    ]);
    mockedUpdateBill.mockResolvedValue({
      id: "bill-1",
      description: "Internet fibra",
      amount: 119.9,
      dueOn: "2026-03-12",
      billSeriesId: null,
      seriesKind: null,
      occurrenceNumber: null,
      totalOccurrences: null,
      isPaid: false,
      paidAtUtc: null,
      createdAtUtc: "2026-03-01T00:00:00Z",
      status: "pending",
    });

    render(<BillsPage />);

    await user.click(await screen.findByRole("button", { name: /^editar$/i }));

    const descriptionInput = screen.getByLabelText("Descricao");
    const amountInput = screen.getByLabelText("Valor");
    const dueOnInput = screen.getByLabelText("Vencimento");

    await user.clear(descriptionInput);
    await user.type(descriptionInput, "Internet fibra");
    await user.clear(amountInput);
    await user.type(amountInput, "119.9");
    await user.clear(dueOnInput);
    await user.type(dueOnInput, "2026-03-12");
    await user.click(screen.getByRole("button", { name: /salvar alteracao/i }));

    await waitFor(() => {
      expect(mockedUpdateBill).toHaveBeenCalledWith("token", "bill-1", {
        description: "Internet fibra",
        amount: 119.9,
        dueOn: "2026-03-12",
      });
    });
  });

  it("Bills_DeleteSeries_UsesSeriesScope", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListBills.mockResolvedValue([
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
    mockedDeleteBill.mockResolvedValue(undefined);

    render(<BillsPage />);

    await user.click(await screen.findByRole("button", { name: /encerrar serie/i }));

    await waitFor(() => {
      expect(mockedDeleteBill).toHaveBeenCalledWith("token", "bill-3", "series");
    });
  });

  it("Bills_EditRecurringOccurrence_ShowsOccurrenceOnlyContract", async () => {
    const user = userEvent.setup();

    mockedUseProtectedSession.mockReturnValue({
      session,
      isLoading: false,
      logout: vi.fn(),
    });

    mockedListBills.mockResolvedValue([
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
    ]);
    mockedUpdateBill.mockResolvedValue({
      id: "bill-2",
      description: "Academia premium",
      amount: 150,
      dueOn: "2026-03-14",
      billSeriesId: "series-2",
      seriesKind: "recurring",
      occurrenceNumber: 2,
      totalOccurrences: 6,
      isPaid: false,
      paidAtUtc: null,
      createdAtUtc: "2026-03-01T00:00:00Z",
      status: "pending",
    });

    render(<BillsPage />);

    await user.click(
      await screen.findByRole("button", { name: /editar ocorrencia/i }),
    );

    expect(
      await screen.findByText(/contrato de edicao da serie/i),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/corrige apenas esta ocorrencia ja criada/i),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/desmarcar pagamento altera so o status desta ocorrencia/i),
    ).toBeInTheDocument();

    const descriptionInput = screen.getByLabelText("Descricao");
    const amountInput = screen.getByLabelText("Valor");
    const dueOnInput = screen.getByLabelText("Vencimento");

    await user.clear(descriptionInput);
    await user.type(descriptionInput, "Academia premium");
    await user.clear(amountInput);
    await user.type(amountInput, "150");
    await user.clear(dueOnInput);
    await user.type(dueOnInput, "2026-03-14");
    await user.click(screen.getByRole("button", { name: /salvar alteracao/i }));

    await waitFor(() => {
      expect(mockedUpdateBill).toHaveBeenCalledWith("token", "bill-2", {
        description: "Academia premium",
        amount: 150,
        dueOn: "2026-03-14",
      });
    });
  });
});
