/**
 * [UC-104] Server trả 403 với code này khi người dùng chưa đồng ý bản Điều khoản / Chính sách bảo mật bắt buộc.
 * apiFetch phát sự kiện, PolicyGate nghe để kiểm tra lại và hiện màn đồng ý (kể cả khi admin vừa công bố bản mới lúc người dùng đang dùng).
 */
export const POLICY_REQUIRED_CODE = "policy_acceptance_required";
export const POLICY_REQUIRED_EVENT = "usas:policy-required";
