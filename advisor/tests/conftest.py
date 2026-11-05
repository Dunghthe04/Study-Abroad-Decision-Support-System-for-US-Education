import os

# Test không được ghi vào file log chat thật, kể cả khi advisor/.env có CHAT_LOG_FILE
os.environ["CHAT_LOG_FILE"] = ""

# Test không được kết nối database thật, kể cả khi advisor/.env có DATABASE_URL
os.environ["DATABASE_URL"] = ""
