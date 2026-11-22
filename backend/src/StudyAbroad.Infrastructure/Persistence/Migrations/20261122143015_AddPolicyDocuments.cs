using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyAbroad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyDocuments : Migration
    {
        private const string InitialSummary = "Bản đầu tiên, chuyển từ nội dung đang có trên hệ thống.";
        private static readonly DateTime InitialAt = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime ChildAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private const string TermsV1 = """
            ## 1. Giới thiệu

            Chào mừng bạn đến với Hệ thống Hỗ trợ Ra Quyết định Du học Mỹ (USAS / USDSS). Khi đăng ký tài khoản và sử dụng dịch vụ của chúng tôi, bạn đồng ý tuân thủ các điều khoản và điều kiện được nêu tại đây.

            ## 2. Quyền và trách nhiệm của người dùng

            - Bạn cam kết cung cấp thông tin học tập, hồ sơ cá nhân và tài chính chính xác, trung thực.
            - Bạn có trách nhiệm bảo mật thông tin đăng nhập và mật khẩu tài khoản cá nhân.
            - Đối với học sinh dưới 16 tuổi, hệ thống yêu cầu sự đồng ý từ cha mẹ hoặc người giám hộ hợp pháp theo quy định của pháp luật.

            ## 3. Khuyến nghị và tư vấn AI

            Các gợi ý trường, phân tích học thuật, dự báo khả năng trúng tuyển và tư vấn từ trợ lý AI mang tính chất định hướng và hỗ trợ ra quyết định. Quyết định nộp hồ sơ và ứng tuyển cuối cùng thuộc về người dùng.

            ## 4. Giới hạn trách nhiệm

            Hệ thống không can thiệp vào quy trình xét tuyển của các trường đại học tại Mỹ và không cam kết kết quả trúng tuyển thực tế của bất kỳ cơ sở đào tạo nào.
            """;

        private const string PrivacyV1 = """
            ## 1. Thu thập dữ liệu

            Chúng tôi chỉ thu thập các thông tin cần thiết phục vụ việc tư vấn du học, bao gồm: họ tên, ngày sinh, email, số điện thoại, điểm số học tập (GPA, chứng chỉ tiếng Anh, SAT/ACT) và ngân sách dự kiến của gia đình.

            ## 2. Bảo vệ dữ liệu trẻ vị thành niên

            Tuân thủ Nghị định 13/2023/NĐ-CP về Bảo vệ dữ liệu cá nhân, đối với dữ liệu của học sinh dưới 16 tuổi, chúng tôi bắt buộc phải có sự đồng ý của cha mẹ hoặc người giám hộ hợp pháp trước khi kích hoạt các tính năng phân tích chuyên sâu.

            ## 3. Sử dụng và chia sẻ thông tin

            - Dữ liệu của bạn chỉ được sử dụng cho thuật toán gợi ý trường và hỗ trợ chuyên gia/AI tư vấn.
            - Chúng tôi tuyệt đối không bán dữ liệu cá nhân cho bên thứ ba. Hồ sơ chỉ được chia sẻ tới trung tâm tư vấn khi bạn chủ động gửi yêu cầu kết nối.

            ## 4. Quyền của người dùng với dữ liệu

            Bạn có toàn quyền xem, chỉnh sửa, yêu cầu trích xuất hoặc xóa vĩnh viễn tài khoản và dữ liệu cá nhân của mình bất kỳ lúc nào trong phần Cài đặt tài khoản.
            """;

        private const string ChildConsentV1 = """
            ## Dữ liệu được xử lý

            - Họ tên, ngày sinh, email, số điện thoại của con.
            - Hồ sơ học thuật: trường, lớp, bảng điểm, chứng chỉ, hoạt động ngoại khóa.
            - Thông tin tài chính du học do gia đình tự khai (nếu nhập).
            - Nội dung con trao đổi với trợ lý tư vấn AI.

            ## Mục đích

            Lưu hồ sơ, phân tích năng lực và gợi ý trường phù hợp cho việc du học Mỹ. Không bán hay chia sẻ dữ liệu cho bên thứ ba; hồ sơ chỉ được gửi cho trung tâm tư vấn khi bạn xác nhận riêng từng lần.

            ## Quyền của phụ huynh

            Rút lại sự đồng ý bất cứ lúc nào bằng link trong email xác nhận gửi sau khi bạn đồng ý (không cần đăng nhập). Khi rút, hệ thống ngừng xử lý dữ liệu của con (con dưới 16 tuổi quay về chế độ hạn chế).

            Căn cứ: Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (Điều 24) và Nghị định 356/2025/NĐ-CP. Hệ thống lưu thời điểm, email nhận thư mời và địa chỉ IP khi bạn trả lời.
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "policy_documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Loại: terms_of_use | privacy_policy | child_data_consent | forum_rules."),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true, comment: "Số phiên bản yyyy-MM (thêm -02, -03 nếu một tháng nhiều bản), đặt lúc công bố. Trống khi còn là nháp."),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, comment: "draft (nháp, sửa được) | published (đang hiệu lực, khóa) | retired (hết hiệu lực, giữ để tra cứu)."),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Tiêu đề hiển thị."),
                    content = table.Column<string>(type: "text", nullable: false, comment: "Toàn văn, Markdown đơn giản. Bản đã công bố không sửa."),
                    change_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Tóm tắt thay đổi so với bản trước; bắt buộc khi công bố, hiện cho người dùng khi được hỏi đồng ý lại."),
                    requires_reconsent = table.Column<bool>(type: "boolean", nullable: false, comment: "Bản này có bắt người dùng đồng ý lại không (UC-104)."),
                    based_on_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Bản được chép ra để tạo nháp này; trống nếu soạn từ đầu."),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Admin tạo bản (nháp). Xóa tài khoản thì để trống."),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Lúc công bố (bắt đầu hiệu lực)."),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Admin công bố. Xóa tài khoản thì để trống."),
                    retired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Lúc hết hiệu lực (có bản mới công bố thay)."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_policy_documents", x => x.id);
                    table.CheckConstraint("ck_policy_documents_draft_shape", "status <> 'draft' OR (version IS NULL AND published_at IS NULL AND retired_at IS NULL)");
                    table.CheckConstraint("ck_policy_documents_kind", "kind IN ('terms_of_use', 'privacy_policy', 'child_data_consent', 'forum_rules')");
                    table.CheckConstraint("ck_policy_documents_published_shape", "status = 'draft' OR (version IS NOT NULL AND published_at IS NOT NULL AND change_summary IS NOT NULL)");
                    table.CheckConstraint("ck_policy_documents_retired_shape", "(status = 'retired') = (retired_at IS NOT NULL)");
                    table.CheckConstraint("ck_policy_documents_status", "status IN ('draft', 'published', 'retired')");
                    table.ForeignKey(
                        name: "fk_policy_documents_policy_documents_based_on_id",
                        column: x => x.based_on_id,
                        principalSchema: "app",
                        principalTable: "policy_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_policy_documents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_policy_documents_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "[UC-103] Các phiên bản chính sách và điều khoản (toàn văn mỗi bản). Bản đã công bố không sửa được; muốn đổi thì tạo bản mới. Mỗi loại chỉ có một bản đang hiệu lực và tối đa một bản nháp.");

            migrationBuilder.CreateIndex(
                name: "ix_policy_documents_based_on_id",
                schema: "app",
                table: "policy_documents",
                column: "based_on_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_documents_created_by_user_id",
                schema: "app",
                table: "policy_documents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_documents_kind_version",
                schema: "app",
                table: "policy_documents",
                columns: new[] { "kind", "version" },
                unique: true,
                filter: "version IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_policy_documents_published_by_user_id",
                schema: "app",
                table: "policy_documents",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_policy_documents_one_draft",
                schema: "app",
                table: "policy_documents",
                column: "kind",
                unique: true,
                filter: "status = 'draft'");

            migrationBuilder.CreateIndex(
                name: "ux_policy_documents_one_published",
                schema: "app",
                table: "policy_documents",
                column: "kind",
                unique: true,
                filter: "status = 'published'");
            // Bản đầu tiên của ba loại tài liệu: chuyển NGUYÊN VĂN từ nội dung đang viết cứng trên giao diện (trang /terms, /privacy và
            // khung nội dung đồng ý của phụ huynh). Phiên bản đồng ý của phụ huynh giữ "2026-10" để khớp các bản ghi consents đã có.
            // Nội quy diễn đàn chưa có văn bản nào nên không nạp; admin soạn bản đầu.
            migrationBuilder.InsertData(
                schema: "app",
                table: "policy_documents",
                columns: ["id", "kind", "version", "status", "title", "content", "change_summary", "requires_reconsent", "published_at", "created_at"],
                values: new object[,]
                {
                    {
                        new Guid("7d1c5a52-1f0e-4f63-9a51-0c3b8f2e1a01"), "terms_of_use", "2026-11", "published", "Điều khoản sử dụng",
                        TermsV1, InitialSummary, false, InitialAt, InitialAt
                    },
                    {
                        new Guid("7d1c5a52-1f0e-4f63-9a51-0c3b8f2e1a02"), "privacy_policy", "2026-11", "published", "Chính sách bảo mật & Quyền riêng tư",
                        PrivacyV1, InitialSummary, false, InitialAt, InitialAt
                    },
                    {
                        new Guid("7d1c5a52-1f0e-4f63-9a51-0c3b8f2e1a03"), "child_data_consent", "2026-10", "published",
                        "Đồng ý xử lý dữ liệu cá nhân của con dưới 16 tuổi", ChildConsentV1, InitialSummary, false, ChildAt, ChildAt
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "policy_documents",
                schema: "app");
        }
    }
}
