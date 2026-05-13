from __future__ import annotations

import unittest

from app.receipt_reader import (
    MAX_RECEIPT_BYTES,
    ReceiptReadError,
    parse_pix_receipt_text,
    validate_receipt_upload,
)


class ReceiptReaderTests(unittest.TestCase):
    def test_parse_pix_receipt_text_complete_receipt_should_return_candidates(self) -> None:
        raw_text = """
        Comprovante de Pix
        Valor: R$ 1.234,56
        Data: 17/04/2026 as 14:35
        Recebedor: Mercado Modelo Ltda
        Pagador: Maria Silva
        ID da transacao: E12345678901234567890123456789012
        """

        candidate = parse_pix_receipt_text(raw_text)

        self.assertEqual("pix_receipt", candidate.documentType)
        self.assertEqual(1234.56, candidate.amount)
        self.assertEqual("2026-04-17T14:35:00", candidate.paidAt)
        self.assertEqual("Mercado Modelo Ltda", candidate.receiverName)
        self.assertEqual("Maria Silva", candidate.payerName)
        self.assertEqual("E12345678901234567890123456789012", candidate.transactionId)
        self.assertGreaterEqual(candidate.confidence, 0.9)
        self.assertIn("Comprovante de Pix", candidate.rawText)

    def test_parse_pix_receipt_text_missing_optional_fields_should_lower_confidence(self) -> None:
        candidate = parse_pix_receipt_text(
            """
            Pix realizado
            Valor R$ 25,90
            Recebedor
            Padaria Central
            """
        )

        self.assertEqual("pix_receipt", candidate.documentType)
        self.assertEqual(25.9, candidate.amount)
        self.assertEqual("Padaria Central", candidate.receiverName)
        self.assertIsNone(candidate.payerName)
        self.assertIsNone(candidate.transactionId)
        self.assertLess(candidate.confidence, 0.9)

    def test_parse_pix_receipt_text_non_pix_text_should_return_unknown_with_low_confidence(self) -> None:
        candidate = parse_pix_receipt_text("Recibo simples sem dados financeiros.")

        self.assertEqual("unknown_receipt", candidate.documentType)
        self.assertEqual(0.0, candidate.amount)
        self.assertEqual(0.0, candidate.confidence)

    def test_parse_pix_receipt_text_should_limit_raw_text(self) -> None:
        candidate = parse_pix_receipt_text("Pix\n" + ("a" * 5000))

        self.assertLessEqual(len(candidate.rawText), 4000)

    def test_validate_receipt_upload_valid_pdf_should_not_raise(self) -> None:
        validate_receipt_upload(b"%PDF-1.7", "comprovante.pdf", "application/pdf")

    def test_validate_receipt_upload_too_large_should_raise(self) -> None:
        with self.assertRaises(ReceiptReadError):
            validate_receipt_upload(
                b"x" * (MAX_RECEIPT_BYTES + 1),
                "comprovante.pdf",
                "application/pdf",
            )

    def test_validate_receipt_upload_invalid_mime_should_raise(self) -> None:
        with self.assertRaises(ReceiptReadError):
            validate_receipt_upload(b"data", "comprovante.txt", "text/plain")

    def test_validate_receipt_upload_extension_mismatch_should_raise(self) -> None:
        with self.assertRaises(ReceiptReadError):
            validate_receipt_upload(b"data", "comprovante.png", "application/pdf")


if __name__ == "__main__":
    unittest.main()
