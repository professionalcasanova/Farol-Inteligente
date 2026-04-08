import type { ComponentProps } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { AppShell } from "@/components/app-shell";

vi.mock("next/link", () => ({
  default: ({ children, href, ...props }: ComponentProps<"a">) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const mockedUsePathname = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => mockedUsePathname(),
}));

const session = {
  accessToken: "token",
  userId: "user-1",
  name: "Marco Antonio",
  email: "marco.antonio@farol.local",
};

describe("AppShell", () => {
  it("AppShell_ActiveNavigation_KeepsCurrentLabelVisible", () => {
    mockedUsePathname.mockReturnValue("/dashboard");

    render(
      <AppShell
        description="Descrição"
        onLogout={vi.fn()}
        session={session}
        title="Visão do mês"
      >
        <div>Conteúdo</div>
      </AppShell>,
    );

    expect(
      screen.getByRole("link", { name: "Dashboard" }),
    ).toHaveAttribute("aria-current", "page");
    expect(screen.getByText("Dashboard")).toBeInTheDocument();
    expect(screen.getByText("Marco Antonio")).toBeInTheDocument();
    expect(screen.getByText("marco.antonio@farol.local")).toBeInTheDocument();
  });

  it("AppShell_ActionsAndSession_AppearInSecondaryRow", () => {
    mockedUsePathname.mockReturnValue("/bills");

    render(
      <AppShell
        actions={<div>Mês das contas</div>}
        utilityActions={<button type="button">Alertas</button>}
        description="Descrição"
        onLogout={vi.fn()}
        session={session}
        title="Contas a pagar"
      >
        <div>Conteúdo</div>
      </AppShell>,
    );

    expect(screen.getByText("Mês das contas")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Alertas" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Sair" })).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Contas a pagar" }),
    ).toHaveAttribute("aria-current", "page");
  });
});
