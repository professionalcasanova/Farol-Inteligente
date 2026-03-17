import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Farol",
  description: "MVP financeiro do Farol para demonstracao de valor real.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
