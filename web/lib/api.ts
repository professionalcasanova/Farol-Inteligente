import type { StoredSession } from "@/lib/auth";

export type TransactionType = 1 | 2;
export type CategoryType = 1 | 2;
export type FinancialAccountType = 1 | 2 | 3 | 4;

export type AuthResponse = StoredSession;

export type AccountResponse = {
  id: string;
  name: string;
  type: FinancialAccountType;
  isActive: boolean;
  createdAtUtc: string;
};

export type CategoryResponse = {
  id: string;
  name: string;
  type: CategoryType;
  isSystem: boolean;
};

export type TransactionResponse = {
  id: string;
  financialAccountId: string;
  categoryId: string | null;
  type: TransactionType;
  amount: number;
  description: string;
  occurredOn: string;
  createdAtUtc: string;
};

export type MonthlySummaryCategoryResponse = {
  categoryId: string | null;
  categoryName: string;
  type: TransactionType;
  total: number;
};

export type MonthlySummaryResponse = {
  month: number;
  year: number;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  byCategory: MonthlySummaryCategoryResponse[];
};

export type MonthlyBudgetCategoryResponse = {
  categoryId: string;
  categoryName: string;
  planned: number;
  spent: number;
  remaining: number;
};

export type MonthlyBudgetResponse = {
  month: number;
  year: number;
  totalPlanned: number;
  totalSpent: number;
  totalRemaining: number;
  categories: MonthlyBudgetCategoryResponse[];
};

export type FreeMoneyResponse = {
  month: number;
  year: number;
  totalIncome: number;
  totalExpense: number;
  balance: number;
  totalPlannedBudget: number;
  totalBudgetSpent: number;
  totalBudgetRemaining: number;
  freeToSpend: number;
};

export type ImportTransactionsCsvErrorResponse = {
  rowNumber: number;
  message: string;
};

export type ImportTransactionsCsvResponse = {
  totalRows: number;
  importedRows: number;
  skippedRows: number;
  errors: ImportTransactionsCsvErrorResponse[];
};

export const transactionTypeOptions: Array<{
  value: TransactionType;
  label: string;
}> = [
  { value: 1, label: "Receita" },
  { value: 2, label: "Despesa" },
];

export const accountTypeOptions: Array<{
  value: FinancialAccountType;
  label: string;
}> = [
  { value: 1, label: "Dinheiro" },
  { value: 2, label: "Conta bancaria" },
  { value: 3, label: "Cartao de credito" },
  { value: 4, label: "Outra" },
];

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "") ??
  "http://localhost:5258";

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

type RequestOptions = {
  method?: string;
  token?: string;
  body?: BodyInit | object;
};

async function apiRequest<T>(path: string, options: RequestOptions = {}) {
  const headers = new Headers();

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  let body = options.body as BodyInit | undefined;

  if (options.body && !(options.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
    body = JSON.stringify(options.body);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body,
  });

  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";
    let message = `Request failed with status ${response.status}.`;

    if (contentType.includes("application/json")) {
      const payload = (await response.json().catch(() => null)) as
        | { message?: string }
        | null;

      if (payload?.message) {
        message = payload.message;
      }
    } else {
      const text = await response.text().catch(() => "");

      if (text) {
        message = text;
      }
    }

    throw new ApiError(message, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function login(email: string, password: string) {
  return apiRequest<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: {
      email,
      password,
    },
  });
}

export async function listAccounts(token: string) {
  return apiRequest<AccountResponse[]>("/api/accounts", { token });
}

export async function createAccount(
  token: string,
  payload: {
    name: string;
    type: FinancialAccountType;
  },
) {
  return apiRequest<AccountResponse>("/api/accounts", {
    method: "POST",
    token,
    body: payload,
  });
}

export async function listCategories(token: string) {
  return apiRequest<CategoryResponse[]>("/api/categories", { token });
}

export async function listTransactions(token: string) {
  return apiRequest<TransactionResponse[]>("/api/transactions", { token });
}

export async function createTransaction(
  token: string,
  payload: {
    financialAccountId: string;
    categoryId?: string;
    type: TransactionType;
    amount: number;
    description: string;
    occurredOn: string;
  },
) {
  return apiRequest<TransactionResponse>("/api/transactions", {
    method: "POST",
    token,
    body: {
      financialAccountId: payload.financialAccountId,
      categoryId: payload.categoryId || null,
      type: payload.type,
      amount: payload.amount,
      description: payload.description,
      occurredOn: payload.occurredOn,
    },
  });
}

export async function getMonthlySummary(
  token: string,
  month: number,
  year: number,
) {
  return apiRequest<MonthlySummaryResponse>(
    `/api/dashboard/monthly-summary?month=${month}&year=${year}`,
    { token },
  );
}

export async function getFreeMoney(token: string, month: number, year: number) {
  return apiRequest<FreeMoneyResponse>(
    `/api/insights/free-money?month=${month}&year=${year}`,
    { token },
  );
}

export async function getMonthlyBudget(
  token: string,
  month: number,
  year: number,
) {
  return apiRequest<MonthlyBudgetResponse>(
    `/api/budgets/monthly?month=${month}&year=${year}`,
    { token },
  );
}

export async function saveMonthlyBudget(
  token: string,
  payload: {
    month: number;
    year: number;
    categories: Array<{
      categoryId: string;
      planned: number;
    }>;
  },
) {
  return apiRequest<MonthlyBudgetResponse>("/api/budgets/monthly", {
    method: "POST",
    token,
    body: payload,
  });
}

export async function importTransactionsCsv(
  token: string,
  payload: {
    financialAccountId: string;
    file: File;
  },
) {
  const formData = new FormData();
  formData.append("financialAccountId", payload.financialAccountId);
  formData.append("file", payload.file);

  return apiRequest<ImportTransactionsCsvResponse>(
    "/api/imports/transactions/csv",
    {
      method: "POST",
      token,
      body: formData,
    },
  );
}
