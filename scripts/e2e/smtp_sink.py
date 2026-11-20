"""Máy chủ SMTP giả dùng khi chạy thử và kiểm thử đầu cuối.

Nhận mọi thư gửi tới, lưu nguyên văn thành file .eml trong thư mục SINK_DIR và không gửi đi đâu.
Không quảng bá AUTH hay STARTTLS, nên API phải chạy với Smtp__EnableSsl=false (run-api.sh đã đặt sẵn).

Chạy: python scripts/e2e/smtp_sink.py   (mặc định 127.0.0.1:2525, thư lưu ở scripts/e2e/.mail)
"""

import asyncio
import os
import re
import time

HOST = os.environ.get("SINK_HOST", "127.0.0.1")
PORT = int(os.environ.get("SINK_PORT", "2525"))
OUT = os.environ.get("SINK_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), ".mail"))


async def handle(reader: asyncio.StreamReader, writer: asyncio.StreamWriter) -> None:
    async def send(line: str) -> None:
        writer.write((line + "\r\n").encode())
        await writer.drain()

    await send("220 usas-sink ESMTP")
    recipients: list[str] = []
    try:
        while True:
            raw = await reader.readline()
            if not raw:
                break
            command = raw.decode(errors="replace").strip()
            upper = command.upper()
            if upper.startswith("EHLO"):
                writer.write(b"250-usas-sink\r\n250-8BITMIME\r\n250-SMTPUTF8\r\n250 OK\r\n")
                await writer.drain()
            elif upper.startswith("HELO"):
                await send("250 usas-sink")
            elif upper.startswith("MAIL FROM"):
                recipients = []
                await send("250 OK")
            elif upper.startswith("RCPT TO"):
                match = re.search(r"<([^>]*)>", command)
                recipients.append((match.group(1) if match else command[8:]).strip().lower())
                await send("250 OK")
            elif upper == "DATA":
                await send("354 End data with <CR><LF>.<CR><LF>")
                data = bytearray()
                while True:
                    line = await reader.readline()
                    if not line or line in (b".\r\n", b".\n"):
                        break
                    if line.startswith(b".."):
                        line = line[1:]
                    data += line
                os.makedirs(OUT, exist_ok=True)
                for recipient in recipients or ["unknown"]:
                    safe = re.sub(r"[^a-z0-9@._-]", "_", recipient)
                    with open(os.path.join(OUT, f"{time.time_ns()}_{safe}.eml"), "wb") as f:
                        f.write(bytes(data))
                await send("250 OK queued")
            elif upper.startswith("QUIT"):
                await send("221 Bye")
                break
            else:  # RSET, NOOP và lệnh khác: chấp nhận cho qua
                await send("250 OK")
    finally:
        writer.close()


async def main() -> None:
    server = await asyncio.start_server(handle, HOST, PORT)
    print(f"SMTP sink on {HOST}:{PORT} -> {OUT}", flush=True)
    async with server:
        await server.serve_forever()


if __name__ == "__main__":
    asyncio.run(main())
