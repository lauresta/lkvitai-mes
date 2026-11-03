using LKvitai.MES.Modules.Shopfloor.Contracts.Reports;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public sealed class RoletaiTemplateTests
{
    [Fact]
    public void ImportedLayout_UsesOnlyAllowedFieldsAndFitsPage()
    {
        Assert.Empty(RoletaiTemplate.Default().Validate());
    }

    [Fact]
    public void PublishValidation_RejectsUnknownTokenAndOverwideColumns()
    {
        var template = RoletaiTemplate.Default();
        template.Sections["rh"].Lines[0].Text = "{ExecuteSql}";
        template.Sections["dt"].Cols[0].W = 40;
        var errors = template.Validate();
        Assert.Contains(errors, error => error.Contains("ExecuteSql"));
        Assert.Contains(errors, error => error.Contains("100 %"));
    }

    [Fact]
    public void PublishValidation_RequiresVisibleDetailAndUniqueFields()
    {
        var template = RoletaiTemplate.Default();
        template.Sections["dt"].Visible = false;
        template.Sections["dt"].Cols.Add(new RoletaiColumn { Key = "Plotis", Label = "Again" });
        Assert.NotEmpty(template.Validate());
    }
}
