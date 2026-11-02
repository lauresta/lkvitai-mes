using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

#pragma warning disable BL0006 // Focused renderer harness exercises real component event handlers.
public sealed class ReferenceAutocompleteTests
{
    [Fact]
    public async Task Filtering_clears_selected_id_and_requires_an_explicit_option_choice()
    {
        await using var view = await Mounted.Create(Items(), value: 1);
        Assert.Equal("Alpha salon", view.Input("value"));
        await view.Event("oninput", new ChangeEventArgs { Value = "Beta" });
        Assert.Null(view.Selected);
        Assert.Single(view.Changes);
        Assert.Null(view.Changes[0]);
        Assert.Equal("Beta", view.Input("value"));
        Assert.Equal("true", view.Input("aria-expanded"));
        Assert.Equal(1, view.OptionCount);
        // Another parent render must not turn typed text into a selected ID.
        await view.Render();
        Assert.Null(view.Selected);
        Assert.Equal("Beta", view.Input("value"));
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(2, view.Selected);
        Assert.Equal("Beta salon", view.Input("value"));
        Assert.Equal("false", view.Input("aria-expanded"));
    }

    [Fact]
    public async Task Keyboard_navigation_and_escape_do_not_commit_a_highlight()
    {
        await using var view = await Mounted.Create(Items());
        await view.Event("onfocus", new FocusEventArgs());
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.NotNull(view.Input("aria-activedescendant"));
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Escape" });
        Assert.Null(view.Selected);
        Assert.Empty(view.Changes);
        Assert.Equal("false", view.Input("aria-expanded"));
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "ArrowUp" });
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(3, view.Selected);
    }

    [Fact]
    public async Task Local_list_is_bounded_and_unmatched_or_numeric_text_does_not_become_an_id()
    {
        var items = Enumerable.Range(1, 100).Select(i => new LiveNamedReferenceDto(i, $"Salon {i:000}")).ToArray();
        await using var view = await Mounted.Create(items);
        await view.Event("onfocus", new FocusEventArgs());
        Assert.Equal(30, view.OptionCount);
        await view.Event("oninput", new ChangeEventArgs { Value = "98" });
        Assert.Equal(1, view.OptionCount);
        Assert.Null(view.Selected);
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(98, view.Selected);
        await view.Event("oninput", new ChangeEventArgs { Value = "missing" });
        Assert.Equal(0, view.OptionCount);
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await view.Event("onblur", new FocusEventArgs());
        Assert.Null(view.Selected);
        Assert.Equal("missing", view.Input("value"));
    }

    [Fact]
    public async Task Historical_selection_preserves_real_id_until_cleared_and_external_reset_updates_text()
    {
        await using var view = await Mounted.Create(Items(), value: -42, existingLabel: "Old branch");
        Assert.Equal("Old branch (saved)", view.Input("value"));
        Assert.Equal(-42, view.Selected);
        await view.Event("onfocus", new FocusEventArgs());
        Assert.Equal(3, view.OptionCount); // Saved fallback is not added to active choices.
        await view.Event("oninput", new ChangeEventArgs { Value = "" });
        Assert.Null(view.Selected);
        await view.Set(nameof(ReferenceAutocomplete.Value), 3);
        Assert.Equal("Gamma salon", view.Input("value"));
        await view.Set(nameof(ReferenceAutocomplete.Value), null);
        Assert.Equal("", view.Input("value"));
    }

    [Theory]
    [InlineData(nameof(ReferenceAutocomplete.Disabled))]
    [InlineData(nameof(ReferenceAutocomplete.ReadOnly))]
    public async Task Disabled_or_readonly_fields_reject_even_dispatched_input_and_keyboard_events(string property)
    {
        await using var view = await Mounted.Create(Items(), value: 1);
        await view.Set(property, true);
        await view.Event("oninput", new ChangeEventArgs { Value = "Beta" });
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await view.Event("onkeydown", new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(1, view.Selected);
        Assert.Equal("Alpha salon", view.Input("value"));
        Assert.Equal("false", view.Input("aria-expanded"));
        Assert.Empty(view.Changes);
    }

    [Fact]
    public async Task Instances_have_distinct_accessible_lists_and_detach_their_own_navigation_listener()
    {
        await using var first = await Mounted.Create(Items());
        var second = await Mounted.Create(Items());
        Assert.NotEqual(first.Input("aria-controls"), second.Input("aria-controls"));
        Assert.Equal("combobox", first.Input("role"));
        Assert.Equal("list", first.Input("aria-autocomplete"));
        await first.Event("oninput", new ChangeEventArgs { Value = "Alpha" });
        Assert.Equal("", second.Input("value"));
        await second.DisposeAsync();
        Assert.Contains("attach", first.Js.Calls);
        Assert.DoesNotContain("detach", first.Js.Calls);
        Assert.Contains("detach", second.Js.Calls);
    }

    private static LiveNamedReferenceDto[] Items() => [new(1, "Alpha salon"), new(2, "Beta salon"), new(3, "Gamma salon")];

    private sealed class Mounted : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly ComponentRenderer _renderer;
        private readonly int _componentId;
        private readonly Dictionary<string, object?> _parameters;
        public readonly FakeJs Js;
        public readonly List<int?> Changes = [];
        public int? Selected => (int?)_parameters[nameof(ReferenceAutocomplete.Value)];
        private Mounted(ServiceProvider services, ComponentRenderer renderer, int id, Dictionary<string, object?> parameters, FakeJs js)
        { _services = services; _renderer = renderer; _componentId = id; _parameters = parameters; Js = js; }
        public static async Task<Mounted> Create(IReadOnlyList<LiveNamedReferenceDto> items, int? value = null, string? existingLabel = null)
        {
            var js = new FakeJs();
            var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(js).BuildServiceProvider();
            var renderer = new ComponentRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var parameters = new Dictionary<string, object?>
            {
                [nameof(ReferenceAutocomplete.Id)] = "reference-test", [nameof(ReferenceAutocomplete.Label)] = "Salon",
                [nameof(ReferenceAutocomplete.Items)] = items, [nameof(ReferenceAutocomplete.Value)] = value,
                [nameof(ReferenceAutocomplete.ExistingLabel)] = existingLabel
            };
            var id = await renderer.Dispatcher.InvokeAsync(() => renderer.Mount());
            var view = new Mounted(services, renderer, id, parameters, js);
            parameters[nameof(ReferenceAutocomplete.ValueChanged)] = EventCallback.Factory.Create<int?>(view, next =>
            { parameters[nameof(ReferenceAutocomplete.Value)] = next; view.Changes.Add(next); });
            await view.Render();
            return view;
        }
        public Task Render() => _renderer.Dispatcher.InvokeAsync(() => _renderer.Render(_componentId, ParameterView.FromDictionary(_parameters)));
        public async Task Set(string name, object? value) { _parameters[name] = value; await Render(); }
        private RenderTreeFrame[] Frames => _renderer.Frames(_componentId).ToArray();
        public object? Input(string name) => InputAttributes().FirstOrDefault(frame => frame.AttributeName == name).AttributeValue;
        private IEnumerable<RenderTreeFrame> InputAttributes()
        {
            var frames = Frames; var index = Array.FindIndex(frames, frame => frame.FrameType == RenderTreeFrameType.Element && frame.ElementName == "input");
            for (var i = index + 1; i < frames.Length && frames[i].FrameType == RenderTreeFrameType.Attribute; i++) yield return frames[i];
        }
        public int OptionCount => Frames.Count(frame => frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "role" && Equals(frame.AttributeValue, "option"));
        public async Task Event(string name, EventArgs args)
        {
            var handler = InputAttributes().Single(frame => frame.AttributeName == name).AttributeEventHandlerId;
            await _renderer.Dispatcher.InvokeAsync(() => _renderer.Send(handler, args));
            await Render();
        }
        public async ValueTask DisposeAsync() { await _renderer.DisposeAsync(); await _services.DisposeAsync(); }
    }

    private sealed class ComponentRenderer(IServiceProvider services, ILoggerFactory logger) : Renderer(services, logger)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public int Mount() => AssignRootComponentId(InstantiateComponent(typeof(ReferenceAutocomplete)));
        public Task Render(int id, ParameterView parameters) => RenderRootComponentAsync(id, parameters);
        public RenderTreeFrame[] Frames(int id) { var frames = GetCurrentRenderTreeFrames(id); return frames.Array.Take(frames.Count).ToArray(); }
        public Task Send(ulong eventId, EventArgs args) => DispatchEventAsync(eventId, null, args);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
    }

    private sealed class FakeJs : IJSRuntime, IJSObjectReference
    {
        public readonly List<string> Calls = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            return ValueTask.FromResult(identifier == "import" ? (TValue)(object)this : default!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
#pragma warning restore BL0006
