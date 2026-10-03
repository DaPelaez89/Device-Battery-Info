using DeviceBatteryInfo.Core;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;

namespace DeviceBatteryInfo.Actions;

internal sealed class RefreshBatteryAction(BatteryPollingService polling) : IActionDefinition
{
    private readonly BatteryPollingService _polling = polling;

    public const string ActionId = "refresh";

    public string Id => ActionId;

    public LocalizedText Name => Strings.Actions.Refresh.Name();

    public LocalizedText Description => Strings.Actions.Refresh.Description();

    public IReadOnlyList<ActionParameter> Parameters { get; } = [];

    public MacroDeckPlatform Platforms => MacroDeckPlatform.All;

    public IActionExecutor CreateExecutor() => new Executor(_polling);

    private sealed class Executor(BatteryPollingService polling) : IActionExecutor
    {
        private readonly BatteryPollingService _polling = polling;

        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            _polling.RequestRefresh();
            return ActionResult.SucceededTask;
        }
    }
}
