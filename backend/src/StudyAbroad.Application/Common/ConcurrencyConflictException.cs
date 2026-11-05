namespace StudyAbroad.Application.Common;

/// <summary>
/// Bản ghi đã bị request khác sửa trong lúc request này đang xử lý (concurrency token xmin không khớp).
/// Repository đổi DbUpdateConcurrencyException sang ngoại lệ này để tầng Application không phụ thuộc EF.
/// </summary>
public class ConcurrencyConflictException(string message, Exception? inner = null) : Exception(message, inner);
