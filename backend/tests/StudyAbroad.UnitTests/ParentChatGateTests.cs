using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using System;
using System.Linq;
using Xunit;

namespace StudyAbroad.UnitTests;

// 1.5e: chat của phụ huynh hỏi "Bạn muốn làm cho con nào?" theo bảng chat trong tài liệu UC76
public class ParentChatGateTests
{
    private static AdvisorChatResponse ProfileTurn() =>
        new("Bạn học lớp mấy?", [], null, Slm: new AdvisorSlmDto(Intents: ["collect_profile"]));

    private static ParentChatChild Managed(string name) => new(Guid.NewGuid(), name, true);
    private static ParentChatChild Linked(string name) => new(Guid.NewGuid(), name, false);

    [Fact]
    public void UnknownChildren_KeepsOldReply()
    {
        var res = ParentChatGate.Apply(ProfileTurn(), null);

        Assert.Equal(ParentChatGate.Answer, res.Answer);
        Assert.Equal(ParentChatGate.ChildrenHref, Assert.Single(res.QuickReplies!).Href);
    }

    [Fact]
    public void NoChild_AsksWhichChild_WithAddChildOnly()
    {
        var res = ParentChatGate.Apply(ProfileTurn(), null, []);

        Assert.Equal(ParentChatGate.NoChildAnswer, res.Answer);
        Assert.Equal(ParentChatGate.AddChildHref, Assert.Single(res.QuickReplies!).Href);
    }

    [Fact]
    public void ManagedChildren_GetOneButtonEach_ThenAddChild()
    {
        var an = Managed("An");
        var binh = Managed("Bình");

        var res = ParentChatGate.Apply(ProfileTurn(), null, [an, binh]);

        Assert.Equal(ParentChatGate.ChooseChildAnswer, res.Answer);
        Assert.Equal(["An", "Bình", "Thêm con"], res.QuickReplies!.Select(r => r.Label));
        Assert.Equal($"/parent/act/{an.Id}", res.QuickReplies![0].Href);
        Assert.Equal(ParentChatGate.AddChildHref, res.QuickReplies![2].Href);
    }

    [Fact]
    public void OnlyLinkedChild_ShowsViewChildren()
    {
        var res = ParentChatGate.Apply(ProfileTurn(), null, [Linked("Hoa")]);

        Assert.Equal(ParentChatGate.Answer, res.Answer);
        Assert.Equal(ParentChatGate.ChildrenHref, Assert.Single(res.QuickReplies!).Href);
    }

    [Fact]
    public void ManagedAndLinked_ShowsAllButtons_NoButtonForLinkedChild()
    {
        var res = ParentChatGate.Apply(ProfileTurn(), null, [Managed("An"), Linked("Hoa")]);

        Assert.Equal(["An", "Thêm con", "Xem ở Con của tôi"], res.QuickReplies!.Select(r => r.Label));
    }

    [Fact]
    public void MaxManagedChildren_HidesAddChild()
    {
        var children = Enumerable.Range(1, ManagedChildService.MaxManagedChildren).Select(i => Managed($"Con {i}")).ToList();

        var res = ParentChatGate.Apply(ProfileTurn(), null, children);

        Assert.DoesNotContain(res.QuickReplies!, r => r.Href == ParentChatGate.AddChildHref);
        Assert.Equal(ManagedChildService.MaxManagedChildren, res.QuickReplies!.Count);
    }

    [Fact]
    public void KnowledgeTurn_IsNotChanged()
    {
        var turn = new AdvisorChatResponse("IELTS là bài thi tiếng Anh", [], null, Slm: new AdvisorSlmDto(Intents: ["knowledge"]));

        Assert.Same(turn, ParentChatGate.Apply(turn, null, []));
    }
}
