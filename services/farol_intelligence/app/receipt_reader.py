from __future__ import annotations

import re
import unicodedata
from dataclasses import asdict, dataclass
from datetime import datetime
from decimal import Decimal, InvalidOperation
from pathlib import PurePath

MAX_RECEIPT_BYTES = 5 * 1024 * 1024
RAW_TEXT_MAX_CHARS = 4000

ALLOWED_MIME_TYPES: dict[str, set[str]] = {
    "application/pdf": {".pdf"},
    "image/jpeg": {".jpg", ".jpeg"},
    "image/png": {".png"},
    "image/webp": {".webp"},
    "image/tiff": {".tif", ".tiff"},
}


@dataclass(frozen=True)
class ReceiptCandidate:
    documentType: str
    amount: float
    paidAt: str | None
    receiverName: str | None
    payerName: str | None
    transactionId: str | None
    confidence: float
    rawText: str

    def to_contract(self) -> dict[str, object]:
        return asdict(self)


class ReceiptReadError(ValueError):
    pass


def validate_receipt_upload(
    file_bytes: bytes,
    filename: str,
    content_type: str,
    *,
    max_bytes: int = MAX_RECEIPT_BYTES,
) -> None:
    if not file_bytes:
        raise ReceiptReadError("Receipt file is empty.")

    if len(file_bytes) > max_bytes:
        raise ReceiptReadError("Receipt file exceeds the configured size limit.")

    normalized_content_type = content_type.strip().lower()
    allowed_extensions = ALLOWED_MIME_TYPES.get(normalized_content_type)
    if allowed_extensions is None:
        raise ReceiptReadError("Receipt MIME type is not allowed.")

    extension = PurePath(filename).suffix.lower()
    if extension not in allowed_extensions:
        raise ReceiptReadError("Receipt extension does not match the MIME type.")


def parse_pix_receipt_text(raw_text: str) -> ReceiptCandidate:
    normalized_raw_text = _normalize_raw_text(raw_text)
    searchable_text = _without_accents(normalized_raw_text).lower()

    amount = _extract_amount(normalized_raw_text)
    paid_at = _extract_paid_at(normalized_raw_text)
    receiver_name = _extract_labeled_name(
        normalized_raw_text,
        ["recebedor", "destinatario", "favorecido"],
    )
    payer_name = _extract_labeled_name(
        normalized_raw_text,
        ["pagador", "remetente", "debitado de"],
    )
    transaction_id = _extract_transaction_id(normalized_raw_text)
    document_type = "pix_receipt" if "pix" in searchable_text else "unknown_receipt"

    confidence = _calculate_confidence(
        is_pix=document_type == "pix_receipt",
        amount=amount,
        paid_at=paid_at,
        receiver_name=receiver_name,
        payer_name=payer_name,
        transaction_id=transaction_id,
    )

    return ReceiptCandidate(
        documentType=document_type,
        amount=float(amount) if amount is not None else 0.0,
        paidAt=paid_at,
        receiverName=receiver_name,
        payerName=payer_name,
        transactionId=transaction_id,
        confidence=confidence,
        rawText=normalized_raw_text[:RAW_TEXT_MAX_CHARS],
    )


def read_receipt_candidates(
    file_bytes: bytes,
    filename: str,
    content_type: str,
) -> ReceiptCandidate:
    validate_receipt_upload(file_bytes, filename, content_type)

    if content_type.strip().lower() == "application/pdf":
        raw_text = _extract_pdf_text(file_bytes)
    else:
        raw_text = _extract_image_text(file_bytes)

    return parse_pix_receipt_text(raw_text)


def _extract_pdf_text(file_bytes: bytes) -> str:
    try:
        import pdfplumber  # type: ignore[import-not-found]
    except ModuleNotFoundError as exception:
        raise ReceiptReadError("PDF extractor is not configured. Install pdfplumber.") from exception

    from io import BytesIO

    with pdfplumber.open(BytesIO(file_bytes)) as pdf:
        page_texts = [page.extract_text() or "" for page in pdf.pages]

    return "\n".join(page_texts)


def _extract_image_text(file_bytes: bytes) -> str:
    try:
        import cv2  # type: ignore[import-not-found]
        import numpy as np  # type: ignore[import-not-found]
        from paddleocr import PaddleOCR  # type: ignore[import-not-found]
    except ModuleNotFoundError as exception:
        raise ReceiptReadError("OCR extractor is not configured. Install PaddleOCR and OpenCV.") from exception

    image_buffer = np.frombuffer(file_bytes, dtype=np.uint8)
    image = cv2.imdecode(image_buffer, cv2.IMREAD_COLOR)
    if image is None:
        raise ReceiptReadError("Receipt image could not be decoded.")

    ocr = PaddleOCR(lang="pt")
    result = ocr.ocr(image)
    lines: list[str] = []

    for page in result or []:
        for item in page or []:
            if len(item) >= 2 and isinstance(item[1], (list, tuple)) and item[1]:
                lines.append(str(item[1][0]))

    return "\n".join(lines)


def _extract_amount(raw_text: str) -> Decimal | None:
    patterns = [
        r"(?:valor|total|transferencia|pagamento)[^\n\r]{0,40}R\$\s*([0-9.]+,[0-9]{2})",
        r"R\$\s*([0-9.]+,[0-9]{2})",
    ]

    for pattern in patterns:
        match = re.search(pattern, raw_text, flags=re.IGNORECASE)
        if match is None:
            continue

        value = match.group(1).replace(".", "").replace(",", ".")
        try:
            return Decimal(value)
        except InvalidOperation:
            return None

    return None


def _extract_paid_at(raw_text: str) -> str | None:
    pattern = r"\b(\d{2}/\d{2}/\d{4})(?:\s*(?:as|às)?\s*(\d{2}:\d{2}(?::\d{2})?))?"
    match = re.search(pattern, raw_text, flags=re.IGNORECASE)
    if match is None:
        return None

    date_part = match.group(1)
    time_part = match.group(2) or "00:00:00"
    if len(time_part) == 5:
        time_part = f"{time_part}:00"

    try:
        paid_at = datetime.strptime(f"{date_part} {time_part}", "%d/%m/%Y %H:%M:%S")
    except ValueError:
        return None

    return paid_at.isoformat()


def _extract_labeled_name(raw_text: str, labels: list[str]) -> str | None:
    lines = [line.strip() for line in raw_text.splitlines() if line.strip()]
    normalized_labels = [_without_accents(label).lower() for label in labels]

    for index, line in enumerate(lines):
        normalized_line = _without_accents(line).lower()
        matched_label = next((label for label in normalized_labels if label in normalized_line), None)
        if matched_label is None:
            continue

        value = _value_after_label(line, matched_label)
        if value is None and index + 1 < len(lines):
            value = lines[index + 1].strip()

        if value is not None and not _looks_like_document_number(value):
            return _clean_name(value)

    return None


def _extract_transaction_id(raw_text: str) -> str | None:
    end_to_end_match = re.search(r"\b(E[0-9A-Z]{20,40})\b", raw_text, flags=re.IGNORECASE)
    if end_to_end_match is not None:
        return end_to_end_match.group(1)

    pattern = (
        r"(?:id da transacao|id da transação|identificador|e2e|endtoend|end to end)"
        r"[^\w]{0,12}([A-Z0-9][A-Z0-9.-]{9,})"
    )
    match = re.search(pattern, raw_text, flags=re.IGNORECASE)
    if match is None:
        return None

    return match.group(1)


def _calculate_confidence(
    *,
    is_pix: bool,
    amount: Decimal | None,
    paid_at: str | None,
    receiver_name: str | None,
    payer_name: str | None,
    transaction_id: str | None,
) -> float:
    score = 0.0
    score += 0.25 if is_pix else 0.0
    score += 0.25 if amount is not None else 0.0
    score += 0.15 if paid_at is not None else 0.0
    score += 0.15 if receiver_name is not None else 0.0
    score += 0.10 if payer_name is not None else 0.0
    score += 0.10 if transaction_id is not None else 0.0

    return round(min(score, 1.0), 2)


def _normalize_raw_text(raw_text: str) -> str:
    return re.sub(r"[ \t]+", " ", raw_text.replace("\r\n", "\n").replace("\r", "\n")).strip()


def _without_accents(value: str) -> str:
    return "".join(
        character
        for character in unicodedata.normalize("NFKD", value)
        if not unicodedata.combining(character)
    )


def _value_after_label(line: str, normalized_label: str) -> str | None:
    normalized_line = _without_accents(line).lower()
    label_index = normalized_line.find(normalized_label)
    if label_index < 0:
        return None

    value_start = label_index + len(normalized_label)
    value = line[value_start:].lstrip(" :-\t")
    return value or None


def _looks_like_document_number(value: str) -> bool:
    digits = re.sub(r"\D", "", value)
    return len(digits) in {11, 14} and len(digits) >= len(value.replace(" ", "")) - 4


def _clean_name(value: str) -> str:
    return re.sub(r"\s+", " ", value).strip(" :-\t")
