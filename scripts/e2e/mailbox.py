"""Đọc thư mà smtp_sink.py đã lưu: lấy thư mới nhất gửi tới một địa chỉ, mã OTP, token lời mời.

Dùng trong script kiểm thử hoặc từ dòng lệnh:
    python scripts/e2e/mailbox.py otp student1@e2e.test
    python scripts/e2e/mailbox.py invite parent1@e2e.test
    python scripts/e2e/mailbox.py last student1@e2e.test
"""

import email
import glob
import html
import os
import re
import sys
import time
from email import policy
from urllib.parse import unquote

MAIL_DIR = os.environ.get("SINK_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mail"))


def _files_for(address: str) -> list[str]:
    safe = re.sub(r"[^a-z0-9@._-]", "_", address.strip().lower())
    return sorted(glob.glob(os.path.join(MAIL_DIR, f"*_{safe}.eml")))


def messages(address: str) -> list[dict]:
    """Mọi thư gửi tới địa chỉ, cũ trước mới sau: {subject, text}."""
    out = []
    for path in _files_for(address):
        with open(path, "rb") as f:
            msg = email.message_from_binary_file(f, policy=policy.default)
        parts = []
        for part in msg.walk():
            if part.get_content_type() in ("text/plain", "text/html"):
                try:
                    parts.append(part.get_content())
                except Exception:  # noqa: BLE001 - thư lỗi mã hóa thì bỏ qua phần đó
                    pass
        out.append({"subject": str(msg.get("Subject", "")), "text": html.unescape("\n".join(parts))})
    return out


def wait_for(address: str, subject_contains: str = "", after_count: int = 0, timeout: float = 20.0) -> dict:
    """Chờ tới khi có thư thứ after_count+1 (tính cả thư cũ) có tiêu đề chứa subject_contains."""
    deadline = time.time() + timeout
    while time.time() < deadline:
        found = [m for m in messages(address) if subject_contains.lower() in m["subject"].lower()]
        if len(found) > after_count:
            return found[-1]
        time.sleep(0.5)
    raise TimeoutError(f"Không thấy thư '{subject_contains}' gửi tới {address} sau {timeout}s")


def plain_text(message: dict) -> str:
    """Nội dung thư bỏ thẻ HTML và khối style (mã màu như #334155 trong CSS không bị nhầm là OTP)."""
    text = re.sub(r"(?is)<style.*?</style>", " ", message["text"])
    text = re.sub(r"<[^>]+>", " ", text)
    return re.sub(r"\s+", " ", text)


def otp(message: dict) -> str:
    match = re.search(r"(?<![#\w])(\d{6})(?!\w)", plain_text(message))
    if not match:
        raise ValueError("Thư không có mã OTP 6 chữ số")
    return match.group(1)


def token_from_link(message: dict, path: str) -> str:
    """Lấy giá trị token=... của đường dẫn chứa path (ví dụ '/family/invite')."""
    match = re.search(re.escape(path) + r"\?token=([^\"'&\s<>]+)", message["text"])
    if not match:
        raise ValueError(f"Thư không có đường dẫn {path}?token=")
    return unquote(match.group(1))


if __name__ == "__main__":
    kind, address = sys.argv[1], sys.argv[2]
    if kind == "otp":
        print(otp(wait_for(address)))
    elif kind == "invite":
        print(token_from_link(wait_for(address, "Gia đình"), "/family/invite"))
    else:
        last = messages(address)[-1]
        print(last["subject"])
        print(last["text"])
