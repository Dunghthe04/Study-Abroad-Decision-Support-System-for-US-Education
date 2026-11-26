# [UC-03 3d] Dat lai xac thuc hai lop cho tai khoan Administrator mat ca dien thoai lan ma khoi phuc.
# Trong he thong chi Administrator dat lai cho Moderator (UC-58 2a); Administrator thi nhom phat trien dat lai bang script nay.
#
# Viec script lam (mot transaction):
#   - Xoa khoa TOTP (users.totp_secret, users.totp_enabled_at)
#   - Vo hieu ma khoi phuc va phieu buoc 2 con hieu luc (otp_tokens purpose recovery_code, login_2fa)
#   - Thu hoi moi phien dang nhap
#   - Ghi mot dong audit_logs action user.two_factor_reset (actor_user_id rong = ngoai he thong)
# Lan dang nhap ke tiep Administrator quet QR moi va nhan 10 ma khoi phuc moi.
#
# Vi du (tu thu muc goc repo):
#   powershell -ExecutionPolicy Bypass -File .\scripts\reset-admin-2fa.ps1 -Email admin@example.com -Reason "Mat dien thoai, xac minh qua cuoc hop nhom"
# Mac dinh chay vao container Postgres local (docker compose service db). Server khac: truyen -Container.

param(
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Reason,
    [string]$Container = "usas-db-1",
    [string]$DbUser = "studyabroad",
    [string]$Database = "studyabroad"
)

$ErrorActionPreference = "Stop"

$Reason = $Reason.Trim()
if ($Reason.Length -eq 0) { throw "Ly do khong duoc de trong." }
if ($Reason.Length -gt 500) { throw "Ly do toi da 500 ky tu." }

# Truyen gia tri qua bien psql (:'email', :'reason') de psql tu dat trong nhay don, khong ghep chuoi SQL
$sql = @'
\set ON_ERROR_STOP 1
BEGIN;
SELECT id AS target_id FROM app.users
 WHERE lower(email) = lower(:'email') AND role = 'admin' AND status <> 'deleted'
 FOR UPDATE \gset
UPDATE app.users SET totp_secret = NULL, totp_enabled_at = NULL, updated_at = now() WHERE id = :'target_id';
UPDATE app.otp_tokens SET used_at = now(), updated_at = now()
 WHERE user_id = :'target_id' AND purpose IN ('recovery_code', 'login_2fa') AND used_at IS NULL;
UPDATE app.user_sessions SET is_revoked = true, updated_at = now() WHERE user_id = :'target_id' AND is_revoked = false;
INSERT INTO app.audit_logs (id, actor_user_id, action, entity_type, entity_id, detail_json, user_agent, created_at)
VALUES (gen_random_uuid(), NULL, 'user.two_factor_reset', 'user', :'target_id',
        jsonb_build_object('userId', :'target_id', 'reason', :'reason', 'source', 'scripts/reset-admin-2fa.ps1'),
        'reset-admin-2fa.ps1', now());
COMMIT;
\echo Da dat lai xac thuc hai lop cho :email
'@

$sql | docker exec -i $Container psql -U $DbUser -d $Database -v "email=$Email" -v "reason=$Reason" -q
if ($LASTEXITCODE -ne 0) {
    throw "Dat lai that bai (khong tim thay Administrator voi email nay, hoac loi ket noi DB). Khong co thay doi nao duoc ghi."
}
