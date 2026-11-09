using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Khớp tên trường học sinh nói với danh sách trường: chỉ tự chọn khi chắc chắn, còn lại hỏi lại hoặc báo không thấy.
public class SchoolNameMatcherTests
{
    // ---------------------------------------------------------------- tìm tên trường TRONG câu hỏi (FindMentioned)

    [Theory]
    [InlineData("học phí Harvard bao nhiêu", "x-harvard")]
    [InlineData("hoc phi harvard bao nhieu", "x-harvard")]                    // không dấu, chữ thường
    [InlineData("NYU có học bổng cho du học sinh không", "x-nyu")]            // viết tắt viết hoa
    [InlineData("UCLA cần IELTS bao nhiêu", "x-ucla")]
    [InlineData("hạn nộp của Stanford University là khi nào", "x-stanford")]
    [InlineData("Boston University xếp hạng bao nhiêu", "x-bu")]              // tên đầy đủ, cụm dài trước
    public void FindMentioned_FindsTheOneSchoolInTheSentence(string question, string code)
    {
        var found = SchoolNameMatcher.FindMentioned(question, Schools);

        Assert.Equal(code, Assert.Single(found).Code);
    }

    [Theory]
    [InlineData("học phí đại học Mỹ bao nhiêu")]            // không nêu trường nào
    [InlineData("học phí Boston bao nhiêu")]                // Boston mơ hồ (BU, BC): không đoán
    [InlineData("em có SAT 1400 và IELTS 7 thì cần học bổng")]   // SAT, IELTS không phải tên trường
    [InlineData("mit")]                                       // viết tắt chữ thường quá ngắn, dễ trùng từ thường
    [InlineData("tiếng Anh cần bao nhiêu")]
    [InlineData("")]
    [InlineData(null)]
    public void FindMentioned_WithoutAClearSchool_FindsNothing(string? question)
    {
        Assert.Empty(SchoolNameMatcher.FindMentioned(question, Schools));
    }

    [Fact]
    public void FindMentioned_ReturnsEachSchoolOnceInTheOrderMentioned()
    {
        var found = SchoolNameMatcher.FindMentioned("học phí MIT và Harvard khác nhau thế nào, Harvard đắt hơn không", Schools);

        Assert.Equal(["x-mit", "x-harvard"], found.Select(s => s.Code));
    }

    [Fact]
    public void FindMentioned_DoesNotGuessATypo()
    {
        Assert.Empty(SchoolNameMatcher.FindMentioned("học phí harvad bao nhiêu", Schools));
    }

    // Danh sách mẫu lấy từ dữ liệu thật, gồm các tên dễ nhầm (Boston, Washington, University of California-*)
    private static readonly IReadOnlyList<SchoolRef> Schools =
    [
        new("x-mit", "Massachusetts Institute of Technology"),
        new("x-bu", "Boston University"),
        new("x-bc", "Boston College"),
        new("x-baylor", "Baylor University"),
        new("x-bucknell", "Bucknell University"),
        new("x-harvard", "Harvard University"),
        new("x-stanford", "Stanford University"),
        new("x-upenn", "University of Pennsylvania"),
        new("x-pennstate", "Pennsylvania State University-Main Campus"),
        new("x-ucla", "University of California-Los Angeles"),
        new("x-ucb", "University of California-Berkeley"),
        new("x-ucsd", "University of California-San Diego"),
        new("x-nyu", "New York University"),
        new("x-usc", "University of Southern California"),
        new("x-cmu", "Carnegie Mellon University"),
        new("x-gatech", "Georgia Institute of Technology-Main Campus"),
        new("x-jhu", "Johns Hopkins University"),
        new("x-gwu", "George Washington University"),
        new("x-washlee", "Washington and Lee University"),
        new("x-columbia", "Columbia University in the City of New York"),
        new("x-michigan", "University of Michigan-Ann Arbor"),
        new("x-utaustin", "The University of Texas at Austin"),
        new("x-utdallas", "The University of Texas at Dallas"),
        new("x-tcu", "Texas Christian University"),
        new("x-northeastern", "Northeastern University"),
        new("x-cornell", "Cornell University"),
    ];

    private static NameMatch Match(string query) => SchoolNameMatcher.Match(query, Schools);

    private static void AssertFound(string query, string code)
    {
        var match = Match(query);
        Assert.Equal(NameMatchKind.Found, match.Kind);
        Assert.Equal("x-" + code, Assert.Single(match.Schools).Code);
    }

    // ---------------------------------------------------------------- khớp chắc chắn

    [Theory]
    [InlineData("Boston University", "bu")]
    [InlineData("boston university", "bu")]
    [InlineData("BOSTON UNIVERSITY", "bu")]
    [InlineData("Harvard", "harvard")]
    [InlineData("harvard university", "harvard")]
    [InlineData("Cornell", "cornell")]
    [InlineData("northeastern", "northeastern")]
    public void FullOrLeadingNames_AreFound(string query, string code) => AssertFound(query, code);

    [Fact]
    public void TheSchoolCode_IsFound()
    {
        // Mã thật là số (mã của Bộ Giáo dục Mỹ): học sinh không gõ, nhưng người vận hành có thể tra bằng mã
        var withCode = Schools.Append(new SchoolRef("188429", "Adelphi University")).ToList();

        var match = SchoolNameMatcher.Match("188429", withCode);

        Assert.Equal(NameMatchKind.Found, match.Kind);
        Assert.Equal("Adelphi University", Assert.Single(match.Schools).Name);
    }

    [Theory]
    [InlineData("MIT", "mit")]
    [InlineData("mit", "mit")]
    [InlineData("NYU", "nyu")]
    [InlineData("USC", "usc")]
    [InlineData("CMU", "cmu")]
    [InlineData("JHU", "jhu")]
    [InlineData("UCLA", "ucla")]
    public void Acronyms_AreFound(string query, string code) => AssertFound(query, code);

    [Theory]
    [InlineData("UCB", "ucb")]               // chữ cái đầu của University of California-Berkeley, duy nhất trong danh sách
    [InlineData("ucsd", "ucsd")]
    public void UniqueAcronymsOfLongNames_AreFound(string query, string code) => AssertFound(query, code);

    // Cách gọi thân thuộc không suy ra được từ tên: không tự chọn, hỏi lại (SLM sẽ được dạy chuẩn hóa thành tên đầy đủ)
    [Theory]
    [InlineData("Penn", new[] { "upenn", "pennstate" })]     // dở từ: Pennsylvania State và University of Pennsylvania
    [InlineData("Berkeley", new[] { "ucb" })]
    [InlineData("Penn State", new[] { "pennstate" })]
    [InlineData("Georgia Tech", new[] { "gatech" })]
    [InlineData("Hopkins", new[] { "jhu" })]
    [InlineData("Michigan", new[] { "michigan" })]
    public void FamiliarNames_AreNotChosenSilently_ButOfferedForConfirmation(string query, string[] expected)
    {
        var match = Match(query);

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Equal(expected.OrderBy(c => c), match.Schools.Select(s => s.Code[2..]).OrderBy(c => c));
    }

    [Fact]
    public void AnAcronymSharedBySeveralSchools_IsAmbiguous_NeverChosenByLuck()
    {
        var match = Match("BU");   // Boston University, Baylor University, Bucknell University

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Equal(["baylor", "bu", "bucknell"], match.Schools.Select(s => s.Code[2..]).OrderBy(c => c));
    }

    [Fact]
    public void ANicknameThatCannotBeDerived_IsNotFound()
    {
        Assert.Equal(NameMatchKind.NotFound, Match("UT Austin").Kind);
    }

    [Theory]
    [InlineData("harvad", "harvard")]        // gõ sai nhẹ
    [InlineData("standford", "stanford")]
    [InlineData("northeastren", "northeastern")]
    public void SmallTypos_AreFound(string query, string code) => AssertFound(query, code);

    [Theory]
    [InlineData("đại học Boston University", "bu")]
    [InlineData("trường MIT", "mit")]
    [InlineData("  Harvard,  University! ", "harvard")]
    public void FillerWordsAndPunctuation_AreIgnored(string query, string code) => AssertFound(query, code);

    [Fact]
    public void ALongNameInTheListMatchesItselfIgnoringCase()
    {
        AssertFound("columbia university in the city of new york", "columbia");
    }

    // ---------------------------------------------------------------- hỏi lại

    [Fact]
    public void ManySchoolsStartingWithTheWords_AreAmbiguous_ShortestNameFirst()
    {
        var match = Match("Boston");

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Equal(["bc", "bu"], match.Schools.Select(s => s.Code[2..]));   // Boston College ngắn hơn Boston University
    }

    [Fact]
    public void SeveralSchoolsSharingTheTypedStart_AreAmbiguous_NotChosenByLuck()
    {
        // The University of Texas at Austin và at Dallas cùng bắt đầu bằng cụm đã gõ: hỏi lại
        var match = Match("The University of Texas");

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        // Hai trường bắt đầu bằng cụm đã gõ xếp trước; trường chỉ chứa các từ đó ở chỗ khác (Texas Christian) xếp sau
        Assert.Equal(["utaustin", "utdallas"], match.Schools.Take(2).Select(s => s.Code[2..]).OrderBy(c => c));
    }

    [Fact]
    public void AMatchOnlyInTheMiddleOfAName_IsNeverChosenSilently()
    {
        // Học sinh nói "Washington University": danh sách không có trường nào bắt đầu bằng cụm đó, nên hỏi lại trong các trường có chữ Washington
        var match = Match("Washington University");

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Contains("gwu", match.Schools.Select(s => s.Code[2..]));
    }

    [Fact]
    public void AMiddleOfNameMatchWithASingleCandidate_StillAsksForConfirmation()
    {
        var match = Match("Hopkins");

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Equal("jhu", Assert.Single(match.Schools).Code[2..]);
    }

    [Fact]
    public void AtMostFiveCandidatesAreOffered()
    {
        var many = Enumerable.Range(0, 12).Select(i => new SchoolRef($"c{i}", $"University of California-Campus {i:00}")).ToList();

        var match = SchoolNameMatcher.Match("University of California", many);

        Assert.Equal(NameMatchKind.Ambiguous, match.Kind);
        Assert.Equal(SchoolNameMatcher.MaxCandidates, match.Schools.Count);
    }

    // ---------------------------------------------------------------- không thấy

    [Theory]
    [InlineData("Yale")]
    [InlineData("Rutgers")]
    [InlineData("Hogwarts")]
    [InlineData("xyz")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UnknownOrEmptyNames_AreNotFound(string? query)
    {
        var match = Match(query!);

        Assert.Equal(NameMatchKind.NotFound, match.Kind);
        Assert.Empty(match.Schools);
    }

    [Fact]
    public void ATooGenericWord_IsNotFoundInsteadOfDozensOfCandidates()
    {
        var many = Enumerable.Range(0, 30).Select(i => new SchoolRef($"c{i}", $"University of Somewhere {i:00}")).ToList();

        Assert.Equal(NameMatchKind.NotFound, SchoolNameMatcher.Match("University", many).Kind);
    }

    [Fact]
    public void ATypoTooBigToTrust_IsNotFound()
    {
        Assert.Equal(NameMatchKind.NotFound, Match("hxrvxrd").Kind);   // sai hai ký tự ở từ ngắn: không đoán
    }

    // ---------------------------------------------------------------- chuẩn hóa

    [Theory]
    [InlineData("Texas A&M", "texas a and m")]
    [InlineData("Johns   Hopkins—University", "johns hopkins university")]
    [InlineData("Đại học Vừa Sức", "dai hoc vua suc")]
    [InlineData(null, "")]
    public void Normalize_LowercasesDropsAccentsAndPunctuation(string? text, string expected) =>
        Assert.Equal(expected, SchoolNameMatcher.Normalize(text));
}
