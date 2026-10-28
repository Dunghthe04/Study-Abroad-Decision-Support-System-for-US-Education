"""Việc B6 bước 1: tìm trung tâm trong bảng study_centers. Dùng hồ chứa kết nối giả nên không cần Postgres."""

from app.services.centers import CITIES_SQL, MAX_CENTERS, SEARCH_SQL, SERVICES, CenterFinder, CenterHit
from app.services.skill_rules import ORDER


class FakeConnection:
    def __init__(self, rows, calls) -> None:
        self._rows, self._calls = rows, calls

    def execute(self, sql, params=None):
        self._calls.append((sql, params))
        return self

    def fetchall(self):
        return self._rows


class FakePool:
    """Hồ chứa giả: trả các dòng soạn sẵn và ghi lại câu SQL cùng tham số."""

    def __init__(self, rows) -> None:
        self._rows = rows
        self.calls: list[tuple] = []

    def connection(self):
        pool = self

        class _Ctx:
            def __enter__(self):
                return FakeConnection(pool._rows, pool.calls)

            def __exit__(self, *exc):
                return False

        return _Ctx()


def test_service_codes_are_upper_snake_case_with_a_vietnamese_label():
    for code, label in SERVICES.items():
        assert code == code.upper() and " " not in code
        assert label.strip()  # tên hiển thị không được rỗng


def test_service_catalog_covers_the_codes_used_by_the_demo_centers():
    # mã trong DbSeeder (CHON_TRUONG, LUYEN_THI, SUA_BAI_LUAN, SAN_HOC_BONG, LUYEN_PHONG_VAN) phải có tên hiển thị
    assert {"CHON_TRUONG", "LUYEN_THI", "SUA_BAI_LUAN", "SAN_HOC_BONG", "LUYEN_PHONG_VAN"} <= set(SERVICES)


def test_b6_skill_is_a_known_skill_label():
    assert "skill_chuyen_trung_tam" in ORDER


async def test_find_returns_centers_and_passes_the_filters_to_sql():
    pool = FakePool(
        [
            ("Trung tâm A", "Hà Nội", "https://a.vn", ["SUA_BAI_LUAN"], ["undergraduate"]),
            ("Trung tâm B", None, None, ["SUA_BAI_LUAN", "CHON_TRUONG"], ["undergraduate", "master"]),
        ]
    )

    hits = await CenterFinder(pool, ["verified"]).find("SUA_BAI_LUAN", "undergraduate", "Hà Nội")

    assert hits == [
        CenterHit("Trung tâm A", "Hà Nội", "https://a.vn", ["SUA_BAI_LUAN"], ["undergraduate"]),
        CenterHit("Trung tâm B", None, None, ["SUA_BAI_LUAN", "CHON_TRUONG"], ["undergraduate", "master"]),
    ]
    sql, params = pool.calls[0]
    assert sql == SEARCH_SQL
    assert params == {
        "statuses": ["verified"],
        "service": "SUA_BAI_LUAN",
        "level": "undergraduate",
        "city": "Hà Nội",
        "limit": MAX_CENTERS,
    }


async def test_find_without_study_level_does_not_filter_by_level():
    pool = FakePool([])

    await CenterFinder(pool, ["verified"]).find("CHON_TRUONG")

    assert pool.calls[0][1]["level"] is None
    assert pool.calls[0][1]["city"] is None  # không nói thành phố thì không ưu tiên nơi nào


def test_search_sql_keeps_the_rules_of_the_framework():
    sql = " ".join(SEARCH_SQL.split())
    assert "is_active" in sql  # đang hoạt động
    assert "verification_status = ANY" in sql  # đã xác minh (trạng thái lấy từ cấu hình)
    assert "ANY(services)" in sql and "ANY(study_levels)" in sql  # đúng dịch vụ, đúng bậc
    # cùng thành phố xếp trước, còn lại theo tên; không xếp hạng chất lượng
    assert "ORDER BY (city IS NOT DISTINCT FROM %(city)s) DESC, name" in sql
    assert MAX_CENTERS == 3


async def test_names_lists_every_center_name():
    pool = FakePool([("Trung tâm A",), ("Trung tâm B",)])

    assert await CenterFinder(pool, ["verified"]).names() == ["Trung tâm A", "Trung tâm B"]


async def test_cities_lists_the_cities_that_have_active_centers():
    pool = FakePool([("Hà Nội",), ("TP.HCM",)])

    assert await CenterFinder(pool, ["verified"]).cities() == ["Hà Nội", "TP.HCM"]
    assert pool.calls[0][0] == CITIES_SQL
