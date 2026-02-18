using System.Collections.Generic;
using FluentAssertions;
using Segment.App.Services;

namespace Segment.Tests
{
    public class PilotSalesTemplateRendererTests
    {
        [Fact]
        public void Render_Should_Bind_Placeholders_From_Data_Map()
        {
            var renderer = new PilotSalesTemplateRenderer();
            var bindings = new Dictionary<string, string>
            {
                ["AgencyName"] = "Acme Legal",
                ["TimeSavedPct"] = "21.50%",
                ["ViolationReductionPct"] = "33.00%"
            };

            string template = "Agency {{AgencyName}} achieved {{TimeSavedPct}} time savings and {{ViolationReductionPct}} quality lift.";
            string rendered = renderer.Render(template, bindings);

            rendered.Should().Be("Agency Acme Legal achieved 21.50% time savings and 33.00% quality lift.");
        }

        [Fact]
        public void Render_Should_Replace_Missing_Bindings_With_Empty_String()
        {
            var renderer = new PilotSalesTemplateRenderer();
            var bindings = new Dictionary<string, string>
            {
                ["AgencyName"] = "Acme Legal"
            };

            string template = "{{AgencyName}} | {{UnknownMetric}}";
            string rendered = renderer.Render(template, bindings);

            rendered.Should().Be("Acme Legal | ");
        }
    }
}
