using GitHub.Copilot.SDK;
using Xunit;

namespace Coralph.Tests;

public sealed class PermissionPolicyTests
{
    [Fact]
    public async Task HandleAsync_NoRestrictions_SafeToolApproves()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("list_open_issues");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_NoRestrictions_DangerousToolDenies()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_DenyListMatch_Denies()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = ["bash"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_DenyListNoMatchForSafeTool_Approves()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = ["bash"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("list_open_issues");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_AllowListMatch_Approves()
    {
        var opt = new LoopOptions { ToolAllow = ["bash", "edit"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_AllowListExplicitlyOverridesDefaultDangerousDeny()
    {
        var opt = new LoopOptions { ToolAllow = ["bash"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_AllowListNoMatch_Denies()
    {
        var opt = new LoopOptions { ToolAllow = ["bash"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("edit");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_DenyTakesPrecedenceOverAllow()
    {
        var opt = new LoopOptions { ToolAllow = ["bash"], ToolDeny = ["bash"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_WildcardDeny_DeniesAll()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = ["*"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_WildcardAllow_ApprovesAll()
    {
        var opt = new LoopOptions { ToolAllow = ["*"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_PrefixWildcardMatch_Approves()
    {
        var opt = new LoopOptions { ToolAllow = ["bash*"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash_command");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_PrefixWildcardNoMatch_Denies()
    {
        var opt = new LoopOptions { ToolAllow = ["bash*"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("edit");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_CaseInsensitiveMatch_Approves()
    {
        var opt = new LoopOptions { ToolAllow = ["BASH"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_CommaSeparatedAllowList_Approves()
    {
        var opt = new LoopOptions { ToolAllow = ["bash,edit,view"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("edit");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_CommaSeparatedDenyList_Denies()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = ["bash,edit,view"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("view");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_EmptyKind_NoAllowList_Approves()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest(string.Empty);

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_NullKind_NoAllowList_Approves()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = new PermissionRequest { Kind = null! };

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_WhitespaceInEntries_Normalized()
    {
        var opt = new LoopOptions { ToolAllow = ["  bash  ", " edit "], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_NullEntriesInList_Skipped()
    {
        var opt = new LoopOptions { ToolAllow = ["bash", "", "  ", "edit"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("edit");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    [Fact]
    public async Task HandleAsync_MultipleRulesInDenyList_FirstMatchWins()
    {
        var opt = new LoopOptions { ToolAllow = null!, ToolDeny = ["bash", "edit", "view"] };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("bash");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertDeniedByRules(result);
    }

    [Fact]
    public async Task HandleAsync_PrefixMatchCaseInsensitive_Approves()
    {
        var opt = new LoopOptions { ToolAllow = ["bash*"], ToolDeny = null! };
        var policy = new PermissionPolicy(opt, eventStream: null);
        var request = CreateRequest("BASH_command");

        var result = await policy.HandleAsync(request, CreateInvocation());

        AssertApproved(result);
    }

    private static PermissionRequest CreateRequest(string kind)
    {
        return new PermissionRequest { Kind = kind };
    }

    private static void AssertApproved(PermissionRequestResult result)
    {
        Assert.Equal(PermissionRequestResultKind.Approved, result.Kind);
    }

    private static void AssertDeniedByRules(PermissionRequestResult result)
    {
        Assert.Equal(PermissionRequestResultKind.DeniedByRules, result.Kind);
    }

    private static PermissionInvocation CreateInvocation()
    {
        return new PermissionInvocation();
    }
}
